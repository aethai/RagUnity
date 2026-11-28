using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ragnarok.Network {
    /// <summary>
    /// SC_NOTIFY_BAN 0x0081 on the live map connection: the server is closing the session (a second login on the
    /// account, a server stop, a kick). The error code goes through the receive-error message (<see cref="MessagePrefix"/>).
    /// </summary>
    public static class RoServerKick {
        public const string MessagePrefix = "SC_NOTIFY_BAN ";

        public static string Message(byte errorCode) => MessagePrefix + errorCode.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public static bool TryParse(string message, out int errorCode) {
            errorCode = -1;
            return message != null && message.StartsWith(MessagePrefix, StringComparison.Ordinal) &&
                int.TryParse(message.Substring(MessagePrefix.Length).Trim(), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out errorCode);
        }
    }

    /// <summary>
    /// ZC_RESTART_ACK 0x00b3 type 1 that the client did not ask for: the server sent the character back to the
    /// character select itself (Hercules chrif charselectreq, e.g. the parallel autotrade plugin after @autotrade). The
    /// original client then goes to the character select; the session ends through the receive-error message.
    /// </summary>
    public static class RoServerRestart {
        public const string Message = "ZC_RESTART_ACK 1";

        public static bool IsCharacterSelect(byte[] frame) => frame != null && frame.Length >= 3 && frame[2] == 1;

        public static bool Is(string message) => message != null && message.StartsWith(Message, StringComparison.Ordinal);
    }

    public sealed class RoNetworkProtocolException : Exception {
        public RoNetworkProtocolException(string message) : base(message) {
        }

        public RoNetworkProtocolException(string message, Exception innerException) : base(message, innerException) {
        }
    }

    public delegate Task RoRuntimeMapLoadBeforeAckHandler(string mapName, int spawnX, int spawnY);

    public enum RoLoginSessionState {
        Disconnected = 0,
        LoginConnected = 1,
        LoginAccepted = 2,
        CharacterConnected = 3,
        CharacterListReceived = 4,
        CharacterCreated = 5,
        MapHandoffReceived = 6,
        MapEntered = 7,
        MapLoadAcknowledged = 8,
        MapMoveRequested = 9,
        Failed = 10
    }

    public sealed class RoInitialCharacterSnapshot {
        public RoInitialCharacterSnapshot(
            RoCharServerAccountAck accountAck,
            RoHcAcceptEnter2 slotLimits,
            RoHcAcceptEnter characterList,
            RoHcNotifyCharList pageInfo,
            RoHcBlockCharacter blockedCharacters,
            RoHcSecondPasswordLogin secondPassword) {
            AccountAck = accountAck;
            SlotLimits = slotLimits;
            CharacterList = characterList;
            PageInfo = pageInfo;
            BlockedCharacters = blockedCharacters;
            SecondPassword = secondPassword;
        }

        public RoCharServerAccountAck AccountAck { get; }
        public RoHcAcceptEnter2 SlotLimits { get; }
        public RoHcAcceptEnter CharacterList { get; }
        public RoHcNotifyCharList PageInfo { get; }
        public RoHcBlockCharacter BlockedCharacters { get; }
        public RoHcSecondPasswordLogin SecondPassword { get; }
        public RoCharacterInfo[] Characters => CharacterList.Characters;
    }

    public sealed class RoLoginSessionResult {
        public RoLoginSessionResult(
            RoServerProfile profile,
            RoAcAcceptLogin3 login,
            RoLoginCharServerInfo selectedCharacterServer,
            RoInitialCharacterSnapshot characters) {
            Profile = profile;
            Login = login;
            SelectedCharacterServer = selectedCharacterServer;
            Characters = characters;
        }

        public RoServerProfile Profile { get; }
        public RoAcAcceptLogin3 Login { get; }
        public RoLoginCharServerInfo SelectedCharacterServer { get; }
        public RoInitialCharacterSnapshot Characters { get; }
    }

    public sealed class RoLoginCreateCharacterResult {
        public RoLoginCreateCharacterResult(
            RoServerProfile profile,
            RoAcAcceptLogin3 login,
            RoLoginCharServerInfo selectedCharacterServer,
            RoInitialCharacterSnapshot initialCharacters,
            RoHcAcceptMakeChar createdCharacter,
            RoHcNotifyZoneServer2 mapHandoff) : this(
            profile,
            login,
            selectedCharacterServer,
            initialCharacters,
            createdCharacter.Character,
            true,
            createdCharacter,
            mapHandoff) {
        }

        public RoLoginCreateCharacterResult(
            RoServerProfile profile,
            RoAcAcceptLogin3 login,
            RoLoginCharServerInfo selectedCharacterServer,
            RoInitialCharacterSnapshot initialCharacters,
            RoCharacterInfo selectedCharacter,
            bool characterWasCreated,
            RoHcNotifyZoneServer2 mapHandoff) : this(
            profile,
            login,
            selectedCharacterServer,
            initialCharacters,
            selectedCharacter,
            characterWasCreated,
            characterWasCreated ? new RoHcAcceptMakeChar(selectedCharacter) : default,
            mapHandoff) {
        }

        private RoLoginCreateCharacterResult(
            RoServerProfile profile,
            RoAcAcceptLogin3 login,
            RoLoginCharServerInfo selectedCharacterServer,
            RoInitialCharacterSnapshot initialCharacters,
            RoCharacterInfo selectedCharacter,
            bool characterWasCreated,
            RoHcAcceptMakeChar createdCharacter,
            RoHcNotifyZoneServer2 mapHandoff) {
            Profile = profile;
            Login = login;
            SelectedCharacterServer = selectedCharacterServer;
            InitialCharacters = initialCharacters;
            SelectedCharacter = selectedCharacter ?? throw new ArgumentNullException(nameof(selectedCharacter));
            CharacterWasCreated = characterWasCreated;
            CreatedCharacter = createdCharacter;
            MapHandoff = mapHandoff;
        }

        public RoServerProfile Profile { get; }
        public RoAcAcceptLogin3 Login { get; }
        public RoLoginCharServerInfo SelectedCharacterServer { get; }
        public RoInitialCharacterSnapshot InitialCharacters { get; }
        public RoCharacterInfo SelectedCharacter { get; }
        public bool CharacterWasCreated { get; }
        public RoHcAcceptMakeChar CreatedCharacter { get; }
        public RoHcNotifyZoneServer2 MapHandoff { get; }

        public RoLoginCreateCharacterResult WithSelectedCharacterSpeed(short speed) {
            if (speed <= 0 || SelectedCharacter.Speed == speed) {
                return this;
            }

            RoCharacterInfo selectedCharacter = RoLoginSessionClient.CloneCharacterInfo(SelectedCharacter);
            selectedCharacter.Speed = speed;
            return new RoLoginCreateCharacterResult(
                Profile,
                Login,
                SelectedCharacterServer,
                InitialCharacters,
                selectedCharacter,
                CharacterWasCreated,
                CreatedCharacter,
                MapHandoff);
        }
    }

    public sealed class RoLoginEnterMapResult {
        public RoLoginEnterMapResult(
            RoLoginCreateCharacterResult characterResult,
            RoZcAid aid,
            RoZcAcceptEnter2 acceptedEnter) : this(characterResult, aid, acceptedEnter, RoMapEntryDrainResult.Empty) {
        }

        public RoLoginEnterMapResult(
            RoLoginCreateCharacterResult characterResult,
            RoZcAid aid,
            RoZcAcceptEnter2 acceptedEnter,
            RoMapEntryDrainResult followUp) : this(characterResult, aid, acceptedEnter, followUp, RoMapEntryDrainResult.Empty) {
        }

        public RoLoginEnterMapResult(
            RoLoginCreateCharacterResult characterResult,
            RoZcAid aid,
            RoZcAcceptEnter2 acceptedEnter,
            RoMapEntryDrainResult followUp,
            RoMapEntryDrainResult initialState) : this(characterResult, aid, acceptedEnter, followUp, initialState, RoMapEntryDrainResult.Empty) {
        }

        public RoLoginEnterMapResult(
            RoLoginCreateCharacterResult characterResult,
            RoZcAid aid,
            RoZcAcceptEnter2 acceptedEnter,
            RoMapEntryDrainResult followUp,
            RoMapEntryDrainResult initialState,
            RoMapEntryDrainResult moveResponse) : this(characterResult, aid, acceptedEnter, followUp, initialState, moveResponse, null) {
        }

        public RoLoginEnterMapResult(
            RoLoginCreateCharacterResult characterResult,
            RoZcAid aid,
            RoZcAcceptEnter2 acceptedEnter,
            RoMapEntryDrainResult followUp,
            RoMapEntryDrainResult initialState,
            RoMapEntryDrainResult moveResponse,
            RoLiveMapSession mapSession) {
            CharacterResult = characterResult ?? throw new ArgumentNullException(nameof(characterResult));
            Aid = aid;
            AcceptedEnter = acceptedEnter;
            FollowUp = followUp ?? RoMapEntryDrainResult.Empty;
            InitialState = initialState ?? RoMapEntryDrainResult.Empty;
            MoveResponse = moveResponse ?? RoMapEntryDrainResult.Empty;
            MapSession = mapSession;
        }

        public RoLoginCreateCharacterResult CharacterResult { get; }
        public RoZcAid Aid { get; }
        public RoZcAcceptEnter2 AcceptedEnter { get; }
        public RoMapEntryDrainResult FollowUp { get; }
        public RoMapEntryDrainResult InitialState { get; }
        public RoMapEntryDrainResult MoveResponse { get; }
        public RoLiveMapSession MapSession { get; }

        public RoLoginEnterMapResult WithSelectedCharacterSpeed(short speed) {
            if (speed <= 0) {
                return this;
            }

            RoLoginCreateCharacterResult characterResult = CharacterResult.WithSelectedCharacterSpeed(speed);
            if (ReferenceEquals(characterResult, CharacterResult)) {
                return this;
            }

            return new RoLoginEnterMapResult(
                characterResult,
                Aid,
                AcceptedEnter,
                FollowUp,
                InitialState,
                MoveResponse,
                MapSession);
        }
    }

    public readonly struct RoMapEntryDrainedPacket {
        public RoMapEntryDrainedPacket(ushort packetId, string packetName, int length, RoPacketFrameStatus status) {
            PacketId = packetId;
            PacketName = packetName ?? string.Empty;
            Length = length;
            Status = status;
        }

        public ushort PacketId { get; }
        public string PacketName { get; }
        public int Length { get; }
        public RoPacketFrameStatus Status { get; }
    }

    public enum RoMapEntryDrainEventKind {
        Unknown = 0,
        PlayerChat = 1,
        TimeNotification = 2,
        ActorEntry = 3,
        ActorMove = 4,
        PlayerMove = 5,
        StopMove = 6,
        ActorVanish = 7,
        ActorDirectionChange = 8,
        ActorSpriteChange = 9,
        GroundItemEntry = 10,
        GroundItemDisappear = 11,
        ActorAction = 12,
        Emotion = 13,
        StatusChange = 14,
        InventoryStart = 15,
        InventoryList = 16,
        InventoryEnd = 17,
        EquipSwitchList = 18,
        ParameterChange = 19,
        StatusSnapshot = 20,
        StatusPropertyChange = 21,
        StatusChangeAck = 22,
        WeightPercentage = 23,
        SkillInfoList = 24,
        ShortcutKeyList = 25,
        WearEquipmentAck = 26,
        TakeoffEquipmentAck = 27,
        ItemThrowAck = 28,
        ItemPickupAck = 29,
        InventoryItemDelete = 30,
        UseItemAck = 124,
        NpcDialogText = 31,
        NpcDialogNext = 32,
        NpcDialogClose = 33,
        NpcDialogMenu = 34,
        NpcDialogNumberInput = 35,
        NpcDialogTextInput = 36,
        NpcDialogClear = 37,
        NpcShopDealType = 38,
        NpcShopBuyList = 39,
        NpcShopSellList = 40,
        NpcShopBuyResult = 41,
        NpcShopSellResult = 42,
        StorageAmount = 43,
        StorageItemAdd = 44,
        StorageItemDelete = 45,
        StorageClose = 46,
        NpcMapMove = 47,
        NpcServerMove = 48,
        NpcCutin = 49,
        SkillUseAck = 50,
        SkillCast = 51,
        SkillDamage = 52,
        SkillResult = 53,
        GroundSkill = 54,
        SkillPostDelay = 55,
        SkillPosition = 56,
        SkillEntry = 57,
        SkillDisappear = 58,
        NotifyEffect = 59,
        AttackFailureForDistance = 60,
        Message = 61,
        QuestList = 62,
        QuestAdd = 63,
        QuestDelete = 64,
        QuestActive = 65,
        QuestObjectiveUpdate = 66,
        PartyOption = 67,
        PartyInfo = 68,
        PartyMemberInfo = 69,
        PartyMemberLeave = 70,
        PartyPosition = 71,
        PartyHp = 72,
        PartyAlive = 73,
        PartyJobAndLevel = 74,
        PartyInvite = 75,
        PartyInviteAck = 76,
        FriendsList = 77,
        FriendState = 78,
        FriendRequest = 79,
        FriendAddResult = 80,
        FriendDelete = 81,
        CartAmount = 82,
        CartOff = 83,
        TradeRequest = 84,
        TradeAck = 85,
        TradeItemAdd = 86,
        TradeAddItemAck = 87,
        TradeConclude = 88,
        TradeCancel = 89,
        TradeSubmit = 90,
        GuildBelongInfo = 91,
        GuildNotice = 92,
        GuildMemberInfo = 93,
        GuildInvite = 94,
        GuildInviteResult = 95,
        GuildLeaveResult = 96,
        GuildChat = 97,
        PetProperty = 98,
        PetFeedResult = 99,
        PetChangeState = 100,
        PetAction = 101,
        HomunculusProperty = 102,
        HomunculusChangeState = 103,
        HomunculusParameterChange = 104,
        HomunculusSkillInfoList = 105,
        MercenaryInfo = 106,
        MercenarySkillInfoList = 107,
        MapProperty = 108,
        MinimapMarker = 109,
        ActorIdentity = 110,
        ActorHpInfo = 111,
        NpcShowScript = 112,
        NpcMarketList = 113,
        NpcMarketResult = 114,
        NpcBarterList = 115,
        NpcExpandedBarterList = 116,
        StatusBaseBonus = 117,
        SkillInfoUpdate = 118,
        SkillInfoAdd = 119,
        SkillInfoUpdate2 = 120,
        AutoRunSkill = 121,
        CartItemAdd = 122,
        CartItemDelete = 123,
        PartyCreateAck = 125,
        PartyConfig = 126,
        PartyLeaderChanged = 127,
        BankCheck = 128,
        BankDepositAck = 129,
        BankWithdrawAck = 130,
        BankOpenAck = 131,
        BankCloseAck = 132,
        ChangeItemOption = 133,
        ItemCompositionList = 134,
        ItemCompositionAck = 135,
        PlayerViewEquipment = 136,
        Whisper = 137,
        WhisperAck = 138,
        RodexList = 139,
        RodexRead = 140,
        RodexSendAck = 141,
        RodexZenyAck = 142,
        RodexItemsAck = 143,
        RodexDeleteAck = 144,
        RodexAddItemAck = 145,
        RodexRemoveItemAck = 146,
        RodexOpenWriteAck = 147,
        RodexCheckNameAck = 148,
        RodexListFailed = 149,
        ActorStateChange = 150,
        AttackRange = 151,
        UiOpen = 152,
        AttendanceResponse = 153,
        UnreadMail = 154,
        ActorResurrection = 155,
        PartyChat = 156,
        ClanChat = 157,
        NpcChat = 158,
        ActionFailure = 159,
        EquipArrow = 160,
        SystemText = 161,
        MakingArrowList = 162,
        TeleportWarpList = 163,
        TeleportResult = 164,
        ItemIdentifyList = 165,
        ItemIdentifyAck = 166,
        SelectCart = 167,
        VendingSetupAllowed = 168,
        VendingBoardAdded = 169,
        VendingBoardRemoved = 170,
        PlayerShopListReceived = 171,
        VendingOpenAccepted = 172,
        VendingSaleReported = 173,
        VendingOpenAck = 174,
        PlayerShopPurchaseResult = 175,
        VendingClosed = 176,
        CastCancel = 177,
        ActorSpirits = 224,
        NotifyEffectById = 178,
        OptionChange2 = 179,
        CharacterNameByGid = 180,
        ChatRoomCreateAck = 181,
        ChatRoomInfo = 182,
        ChatRoomDestroy = 183,
        ChatRoomRefuse = 184,
        ChatRoomEnter = 185,
        ChatRoomMemberJoin = 186,
        ChatRoomMemberExit = 187,
        ChatRoomRole = 188,
        BuyingStoreSetup = 189,
        BuyingStoreOpenFailed = 190,
        BuyingStoreOwnList = 191,
        BuyingStoreEntry = 192,
        BuyingStoreDisappear = 193,
        BuyingStoreItemList = 194,
        BuyingStoreBuyerFailed = 195,
        BuyingStoreSold = 196,
        BuyingStoreSellerFailed = 197,
        BuyingStoreUpdate = 198,
        RepairItemList = 199,
        RepairItemResult = 200,
        WeaponRefineList = 201,
        WeaponRefineResult = 202,
        AutoSpellList = 203,
        MergeItemOpen = 204,
        MergeItemResult = 205,
        MakableItemList = 206,
        MakingItemResult = 207,
        PetEggList = 208,
        PetCaptureStart = 209,
        PetCaptureResult = 210,
        MercenaryParameterChange = 211,
        HomunculusSkillUpdate = 212,
        GuildManager = 213,
        PetEvolutionResult = 214,
        ServerMapMark = 215,
        PvpRanking = 216,
        BattlefieldScore = 217,
        NavigationActive = 218,
        BattlefieldChat = 219,
        EntryQueue = 220,
        BattlefieldCampInfo = 221,
        BattlefieldPosition = 222,
        BattlefieldHp = 223
    }

    public sealed class RoMapEntryDrainEvent {
        public RoMapEntryDrainEvent(RoZcNotifyPlayerChat playerChat) : this(RoMapEntryDrainEventKind.PlayerChat) {
            payload = playerChat;
        }

        public RoMapEntryDrainEvent(RoZcNpcChat npcChat) : this(RoMapEntryDrainEventKind.NpcChat) {
            payload = npcChat;
        }

        public RoMapEntryDrainEvent(RoZcSystemText systemText) : this(RoMapEntryDrainEventKind.SystemText) {
            payload = systemText;
        }

        public RoMapEntryDrainEvent(RoZcWhisper whisper) : this(RoMapEntryDrainEventKind.Whisper) {
            payload = whisper;
        }

        public RoMapEntryDrainEvent(RoZcWhisperAck whisperAck) : this(RoMapEntryDrainEventKind.WhisperAck) {
            payload = whisperAck;
        }

        public RoMapEntryDrainEvent(RoZcMessage message) : this(RoMapEntryDrainEventKind.Message) {
            payload = message;
        }

        public RoMapEntryDrainEvent(RoZcQuestList questList) : this(RoMapEntryDrainEventKind.QuestList) {
            payload = questList;
        }

        public RoMapEntryDrainEvent(RoZcQuestAdd questAdd) : this(RoMapEntryDrainEventKind.QuestAdd) {
            payload = questAdd;
        }

        public RoMapEntryDrainEvent(RoZcQuestDelete questDelete) : this(RoMapEntryDrainEventKind.QuestDelete) {
            payload = questDelete;
        }

        public RoMapEntryDrainEvent(RoZcQuestActive questActive) : this(RoMapEntryDrainEventKind.QuestActive) {
            payload = questActive;
        }

        public RoMapEntryDrainEvent(RoZcQuestObjectiveUpdate questObjectiveUpdate) : this(RoMapEntryDrainEventKind.QuestObjectiveUpdate) {
            payload = questObjectiveUpdate;
        }

        public RoMapEntryDrainEvent(RoZcPartyOption partyOption) : this(RoMapEntryDrainEventKind.PartyOption) {
            payload = partyOption;
        }

        public RoMapEntryDrainEvent(RoZcPartyCreateAck partyCreateAck) : this(RoMapEntryDrainEventKind.PartyCreateAck) {
            payload = partyCreateAck;
        }

        public RoMapEntryDrainEvent(RoZcPartyConfig partyConfig) : this(RoMapEntryDrainEventKind.PartyConfig) {
            payload = partyConfig;
        }

        public RoMapEntryDrainEvent(RoZcPartyLeaderChanged partyLeaderChanged) : this(RoMapEntryDrainEventKind.PartyLeaderChanged) {
            payload = partyLeaderChanged;
        }

        public RoMapEntryDrainEvent(RoZcPartyInfo partyInfo) : this(RoMapEntryDrainEventKind.PartyInfo) {
            payload = partyInfo;
        }

        public RoMapEntryDrainEvent(RoZcPartyMemberInfo partyMemberInfo) : this(RoMapEntryDrainEventKind.PartyMemberInfo) {
            payload = partyMemberInfo;
        }

        public RoMapEntryDrainEvent(RoZcPartyMemberLeave partyMemberLeave) : this(RoMapEntryDrainEventKind.PartyMemberLeave) {
            payload = partyMemberLeave;
        }

        public RoMapEntryDrainEvent(RoZcPartyPosition partyPosition) : this(RoMapEntryDrainEventKind.PartyPosition) {
            payload = partyPosition;
        }

        public RoMapEntryDrainEvent(RoZcPartyHp partyHp) : this(RoMapEntryDrainEventKind.PartyHp) {
            payload = partyHp;
        }

        public RoMapEntryDrainEvent(RoZcPartyAlive partyAlive) : this(RoMapEntryDrainEventKind.PartyAlive) {
            payload = partyAlive;
        }

        public RoMapEntryDrainEvent(RoZcPartyJobAndLevel partyJobAndLevel) : this(RoMapEntryDrainEventKind.PartyJobAndLevel) {
            payload = partyJobAndLevel;
        }

        public RoMapEntryDrainEvent(RoZcPartyInvite partyInvite) : this(RoMapEntryDrainEventKind.PartyInvite) {
            payload = partyInvite;
        }

        public RoMapEntryDrainEvent(RoZcPartyInviteAck partyInviteAck) : this(RoMapEntryDrainEventKind.PartyInviteAck) {
            payload = partyInviteAck;
        }

        public RoMapEntryDrainEvent(RoZcFriendsList friendsList) : this(RoMapEntryDrainEventKind.FriendsList) {
            payload = friendsList;
        }

        public RoMapEntryDrainEvent(RoZcFriendState friendState) : this(RoMapEntryDrainEventKind.FriendState) {
            payload = friendState;
        }

        public RoMapEntryDrainEvent(RoZcFriendRequest friendRequest) : this(RoMapEntryDrainEventKind.FriendRequest) {
            payload = friendRequest;
        }

        public RoMapEntryDrainEvent(RoZcFriendAddResult friendAddResult) : this(RoMapEntryDrainEventKind.FriendAddResult) {
            payload = friendAddResult;
        }

        public RoMapEntryDrainEvent(RoZcFriendDelete friendDelete) : this(RoMapEntryDrainEventKind.FriendDelete) {
            payload = friendDelete;
        }

        public RoMapEntryDrainEvent(RoZcAckReqNameByGid characterNameByGid) : this(RoMapEntryDrainEventKind.CharacterNameByGid) {
            payload = characterNameByGid;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcChatRoomResult result) : this(kind) {
            ChatRoomResult = result;
        }

        public RoMapEntryDrainEvent(RoChatRoomInfo chatRoomInfo) : this(RoMapEntryDrainEventKind.ChatRoomInfo) {
            payload = chatRoomInfo;
        }

        public RoMapEntryDrainEvent(uint destroyedChatId) : this(RoMapEntryDrainEventKind.ChatRoomDestroy) {
            payload = destroyedChatId;
        }

        public RoMapEntryDrainEvent(RoZcChatRoomEnter chatRoomEnter) : this(RoMapEntryDrainEventKind.ChatRoomEnter) {
            payload = chatRoomEnter;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcChatRoomMemberChange member) : this(kind) {
            ChatRoomMember = member;
        }

        public RoMapEntryDrainEvent(RoZcChatRoomRole chatRoomRole) : this(RoMapEntryDrainEventKind.ChatRoomRole) {
            payload = chatRoomRole;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcBuyingStoreResult result) : this(kind) {
            BuyingStoreResult = result;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcBuyingStoreList list) : this(kind) {
            BuyingStoreList = list;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcBuyingStoreEntry entry) : this(kind) {
            BuyingStoreEntry = entry;
        }

        public RoMapEntryDrainEvent(RoZcBuyingStoreSold sold) : this(RoMapEntryDrainEventKind.BuyingStoreSold) {
            payload = sold;
        }

        public RoMapEntryDrainEvent(RoZcBuyingStoreUpdate update) : this(RoMapEntryDrainEventKind.BuyingStoreUpdate) {
            payload = update;
        }

        public RoMapEntryDrainEvent(RoZcRepairItemList repairList) : this(RoMapEntryDrainEventKind.RepairItemList) {
            payload = repairList;
        }

        public RoMapEntryDrainEvent(RoZcAckItemRepair repairResult) : this(RoMapEntryDrainEventKind.RepairItemResult) {
            payload = repairResult;
        }

        public RoMapEntryDrainEvent(RoZcAckWeaponRefine weaponRefineResult) : this(RoMapEntryDrainEventKind.WeaponRefineResult) {
            payload = weaponRefineResult;
        }

        public RoMapEntryDrainEvent(RoZcAutoSpellList autoSpellList) : this(RoMapEntryDrainEventKind.AutoSpellList) {
            payload = autoSpellList;
        }

        public RoMapEntryDrainEvent(RoZcMergeItemOpen mergeItemOpen) : this(RoMapEntryDrainEventKind.MergeItemOpen) {
            payload = mergeItemOpen;
        }

        public RoMapEntryDrainEvent(RoZcAckMergeItem mergeItemResult) : this(RoMapEntryDrainEventKind.MergeItemResult) {
            payload = mergeItemResult;
        }

        public RoMapEntryDrainEvent(RoZcPetEggList petEggList) : this(RoMapEntryDrainEventKind.PetEggList) {
            payload = petEggList;
        }

        public static RoMapEntryDrainEvent PetCapture(bool start, byte result) =>
            new RoMapEntryDrainEvent(start ? RoMapEntryDrainEventKind.PetCaptureStart : RoMapEntryDrainEventKind.PetCaptureResult) { PetCaptureResult = result };

        public static RoMapEntryDrainEvent PetEvolution(uint result) =>
            new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.PetEvolutionResult) { PetEvolutionResult = result };

        /// <summary>A minimap marker packet (guild position, quest NPC mark, boss position, ping): one of the four is set.</summary>
        public static RoMapEntryDrainEvent Marker(RoZcGuildPosition guild) =>
            new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.ServerMapMark) { GuildPosition = guild, MarkerKind = 1 };

        public static RoMapEntryDrainEvent Marker(RoZcQuestNotifyEffect quest) =>
            new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.ServerMapMark) { QuestNotifyEffect = quest, MarkerKind = 2 };

        public static RoMapEntryDrainEvent Marker(RoZcBossInfo boss) =>
            new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.ServerMapMark) { BossInfo = boss, MarkerKind = 3 };

        public static RoMapEntryDrainEvent Marker(RoZcMinimapPing ping) =>
            new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.ServerMapMark) { MinimapPing = ping, MarkerKind = 4 };

        public RoMapEntryDrainEvent(RoZcMakableItemList makableItemList) : this(RoMapEntryDrainEventKind.MakableItemList) {
            payload = makableItemList;
        }

        public RoMapEntryDrainEvent(RoZcAckReqMakingItem makingItemResult) : this(RoMapEntryDrainEventKind.MakingItemResult) {
            payload = makingItemResult;
        }

        public static RoMapEntryDrainEvent WeaponRefineItems(RoZcRepairItemList list) => new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.WeaponRefineList) { WeaponRefineList = list };

        public RoMapEntryDrainEvent(RoZcNotifyTime timeNotification) : this(RoMapEntryDrainEventKind.TimeNotification) {
            payload = timeNotification;
        }

        public RoMapEntryDrainEvent(RoZcNotifyActorEntry actorEntry) : this(RoMapEntryDrainEventKind.ActorEntry) {
            payload = actorEntry;
        }

        public RoMapEntryDrainEvent(RoZcNotifyMove actorMove) : this(RoMapEntryDrainEventKind.ActorMove) {
            payload = actorMove;
        }

        public RoMapEntryDrainEvent(RoZcNotifyPlayerMove playerMove) : this(RoMapEntryDrainEventKind.PlayerMove) {
            payload = playerMove;
        }

        public RoMapEntryDrainEvent(RoZcStopMove stopMove) : this(RoMapEntryDrainEventKind.StopMove) {
            payload = stopMove;
        }

        public RoMapEntryDrainEvent(RoZcActorIdentity actorIdentity) : this(RoMapEntryDrainEventKind.ActorIdentity) {
            payload = actorIdentity;
        }

        public RoMapEntryDrainEvent(RoZcHpInfo actorHpInfo) : this(RoMapEntryDrainEventKind.ActorHpInfo) {
            payload = actorHpInfo;
        }

        public RoMapEntryDrainEvent(RoZcNotifyVanish actorVanish) : this(RoMapEntryDrainEventKind.ActorVanish) {
            payload = actorVanish;
        }

        public RoMapEntryDrainEvent(RoZcResurrection actorResurrection) : this(RoMapEntryDrainEventKind.ActorResurrection) {
            payload = actorResurrection;
        }

        public RoMapEntryDrainEvent(RoZcChangeDirection actorDirectionChange) : this(RoMapEntryDrainEventKind.ActorDirectionChange) {
            payload = actorDirectionChange;
        }

        public RoMapEntryDrainEvent(RoZcSpriteChange actorSpriteChange) : this(RoMapEntryDrainEventKind.ActorSpriteChange) {
            payload = actorSpriteChange;
        }

        public RoMapEntryDrainEvent(RoZcItemEntry groundItemEntry) : this(RoMapEntryDrainEventKind.GroundItemEntry) {
            payload = groundItemEntry;
        }

        public RoMapEntryDrainEvent(RoZcItemDisappear groundItemDisappear) : this(RoMapEntryDrainEventKind.GroundItemDisappear) {
            payload = groundItemDisappear;
        }

        public RoMapEntryDrainEvent(RoZcNotifyAct actorAction) : this(RoMapEntryDrainEventKind.ActorAction) {
            payload = actorAction;
        }

        public RoMapEntryDrainEvent(RoZcEmotion emotion) : this(RoMapEntryDrainEventKind.Emotion) {
            payload = emotion;
        }

        public RoMapEntryDrainEvent(RoZcMsgStateChange statusChange) : this(RoMapEntryDrainEventKind.StatusChange) {
            payload = statusChange;
        }

        public RoMapEntryDrainEvent(RoZcStateChange actorStateChange) : this(RoMapEntryDrainEventKind.ActorStateChange) {
            payload = actorStateChange;
        }

        public RoMapEntryDrainEvent(RoZcInventoryStart inventoryStart) : this(RoMapEntryDrainEventKind.InventoryStart) {
            payload = inventoryStart;
        }

        public RoMapEntryDrainEvent(RoZcInventoryItemList inventoryList) : this(RoMapEntryDrainEventKind.InventoryList) {
            payload = inventoryList;
        }

        public RoMapEntryDrainEvent(RoZcInventoryEnd inventoryEnd) : this(RoMapEntryDrainEventKind.InventoryEnd) {
            payload = inventoryEnd;
        }

        public RoMapEntryDrainEvent(RoZcEquipSwitchList equipSwitchList) : this(RoMapEntryDrainEventKind.EquipSwitchList) {
            payload = equipSwitchList;
        }

        public RoMapEntryDrainEvent(RoZcBattlefieldChat battlefieldChat) : this(RoMapEntryDrainEventKind.BattlefieldChat) {
            payload = battlefieldChat;
        }

        public RoMapEntryDrainEvent(RoZcEntryQueue entryQueue) : this(RoMapEntryDrainEventKind.EntryQueue) {
            payload = entryQueue;
        }

        public RoMapEntryDrainEvent(RoZcNavigationActive navigationActive) : this(RoMapEntryDrainEventKind.NavigationActive) {
            payload = navigationActive;
        }

        public RoMapEntryDrainEvent(RoZcEquipWinMicroscope playerViewEquipment) : this(RoMapEntryDrainEventKind.PlayerViewEquipment) {
            payload = playerViewEquipment;
        }

        public RoMapEntryDrainEvent(RoZcParameterChange parameterChange) : this(RoMapEntryDrainEventKind.ParameterChange) {
            payload = parameterChange;
        }

        public RoMapEntryDrainEvent(RoZcStatusSnapshot statusSnapshot) : this(RoMapEntryDrainEventKind.StatusSnapshot) {
            payload = statusSnapshot;
        }

        public RoMapEntryDrainEvent(RoZcStatusPropertyChange statusPropertyChange) : this(RoMapEntryDrainEventKind.StatusPropertyChange) {
            payload = statusPropertyChange;
        }

        public RoMapEntryDrainEvent(RoZcStatusChangeAck statusChangeAck) : this(RoMapEntryDrainEventKind.StatusChangeAck) {
            payload = statusChangeAck;
        }

        public RoMapEntryDrainEvent(RoZcCoupleStatus statusBaseBonus) : this(RoMapEntryDrainEventKind.StatusBaseBonus) {
            payload = statusBaseBonus;
        }

        public RoMapEntryDrainEvent(RoZcNotifyWeightPercentage weightPercentage) : this(RoMapEntryDrainEventKind.WeightPercentage) {
            payload = weightPercentage;
        }

        public RoMapEntryDrainEvent(RoZcNotifyEffectById notifyEffectById) : this(RoMapEntryDrainEventKind.NotifyEffectById) {
            payload = notifyEffectById;
        }

        public RoMapEntryDrainEvent(RoZcOptionChange2 optionChange2) : this(RoMapEntryDrainEventKind.OptionChange2) {
            payload = optionChange2;
        }

        public RoMapEntryDrainEvent(RoZcDispel castCancel) : this(RoMapEntryDrainEventKind.CastCancel) {
            payload = castCancel;
        }

        public RoMapEntryDrainEvent(RoZcSpirits spirits) : this(RoMapEntryDrainEventKind.ActorSpirits) {
            payload = spirits;
        }

        public RoMapEntryDrainEvent(RoZcAttackRange attackRange) : this(RoMapEntryDrainEventKind.AttackRange) {
            payload = attackRange;
        }

        public RoMapEntryDrainEvent(RoZcSkillInfoList skillInfoList) : this(RoMapEntryDrainEventKind.SkillInfoList) {
            payload = skillInfoList;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcSkillInfo skillInfo) : this(kind) {
            SkillInfo = skillInfo;
        }

        public RoMapEntryDrainEvent(RoZcShortcutKeyList shortcutKeyList) : this(RoMapEntryDrainEventKind.ShortcutKeyList) {
            payload = shortcutKeyList;
        }

        public RoMapEntryDrainEvent(RoZcWearEquipmentAck wearEquipmentAck) : this(RoMapEntryDrainEventKind.WearEquipmentAck) {
            payload = wearEquipmentAck;
        }

        public RoMapEntryDrainEvent(RoZcTakeoffEquipmentAck takeoffEquipmentAck) : this(RoMapEntryDrainEventKind.TakeoffEquipmentAck) {
            payload = takeoffEquipmentAck;
        }

        public RoMapEntryDrainEvent(RoZcEquipArrow equipArrow) : this(RoMapEntryDrainEventKind.EquipArrow) {
            payload = equipArrow;
        }

        public RoMapEntryDrainEvent(RoZcItemThrowAck itemThrowAck) : this(RoMapEntryDrainEventKind.ItemThrowAck) {
            payload = itemThrowAck;
        }

        public RoMapEntryDrainEvent(RoZcItemPickupAck itemPickupAck) : this(RoMapEntryDrainEventKind.ItemPickupAck) {
            payload = itemPickupAck;
        }

        public RoMapEntryDrainEvent(RoZcInventoryItemDelete inventoryItemDelete) : this(RoMapEntryDrainEventKind.InventoryItemDelete) {
            payload = inventoryItemDelete;
        }

        public RoMapEntryDrainEvent(RoZcUseItemAck useItemAck) : this(RoMapEntryDrainEventKind.UseItemAck) {
            payload = useItemAck;
        }

        public RoMapEntryDrainEvent(RoZcItemCompositionList itemCompositionList) : this(RoMapEntryDrainEventKind.ItemCompositionList) {
            payload = itemCompositionList;
        }

        public RoMapEntryDrainEvent(RoZcItemCompositionAck itemCompositionAck) : this(RoMapEntryDrainEventKind.ItemCompositionAck) {
            payload = itemCompositionAck;
        }

        public RoMapEntryDrainEvent(RoZcItemIdentifyList itemIdentifyList) : this(RoMapEntryDrainEventKind.ItemIdentifyList) {
            payload = itemIdentifyList;
        }

        public RoMapEntryDrainEvent(RoZcItemIdentifyAck itemIdentifyAck) : this(RoMapEntryDrainEventKind.ItemIdentifyAck) {
            payload = itemIdentifyAck;
        }

        public RoMapEntryDrainEvent(RoZcMakingArrowList makingArrowList) : this(RoMapEntryDrainEventKind.MakingArrowList) {
            payload = makingArrowList;
        }

        public RoMapEntryDrainEvent(RoZcSelectCart selectCart) : this(RoMapEntryDrainEventKind.SelectCart) {
            payload = selectCart;
        }

        public RoMapEntryDrainEvent(RoZcWarpList teleportWarpList) : this(RoMapEntryDrainEventKind.TeleportWarpList) {
            payload = teleportWarpList;
        }

        public RoMapEntryDrainEvent(RoZcNotifyMapInfo teleportResult) : this(RoMapEntryDrainEventKind.TeleportResult) {
            payload = teleportResult;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcNpcDialogText npcDialogText) : this(kind) {
            NpcDialogText = npcDialogText;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcNpcDialogSignal npcDialogSignal) : this(kind) {
            NpcDialogSignal = npcDialogSignal;
        }

        public RoMapEntryDrainEvent(RoZcSelectDealType shopDealType) : this(RoMapEntryDrainEventKind.NpcShopDealType) {
            payload = shopDealType;
        }

        public RoMapEntryDrainEvent(RoZcNpcShopBuyList shopBuyList) : this(RoMapEntryDrainEventKind.NpcShopBuyList) {
            payload = shopBuyList;
        }

        public RoMapEntryDrainEvent(RoZcNpcShopSellList shopSellList) : this(RoMapEntryDrainEventKind.NpcShopSellList) {
            payload = shopSellList;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcNpcShopResult shopResult) : this(kind) {
            NpcShopResult = shopResult;
        }

        public RoMapEntryDrainEvent(RoZcOpenStore openStore) : this(RoMapEntryDrainEventKind.VendingSetupAllowed) {
            payload = openStore;
        }

        public RoMapEntryDrainEvent(RoZcStoreEntry storeEntry) : this(RoMapEntryDrainEventKind.VendingBoardAdded) {
            payload = storeEntry;
        }

        public RoMapEntryDrainEvent(RoZcDisappearEntry disappearEntry) : this(RoMapEntryDrainEventKind.VendingBoardRemoved) {
            payload = disappearEntry;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcPlayerVendingItemList vendingItemList) : this(kind) {
            VendingItemList = vendingItemList;
        }

        public RoMapEntryDrainEvent(RoZcPlayerVendingPurchaseResult vendingPurchaseResult) : this(RoMapEntryDrainEventKind.PlayerShopPurchaseResult) {
            payload = vendingPurchaseResult;
        }

        public RoMapEntryDrainEvent(RoZcDeleteItemFromMcStore vendingSaleReport) : this(RoMapEntryDrainEventKind.VendingSaleReported) {
            payload = vendingSaleReport;
        }

        public RoMapEntryDrainEvent(RoZcAckOpenStore2 vendingOpenAck) : this(RoMapEntryDrainEventKind.VendingOpenAck) {
            payload = vendingOpenAck;
        }

        public RoMapEntryDrainEvent(RoZcNpcMarketList npcMarketList) : this(RoMapEntryDrainEventKind.NpcMarketList) {
            payload = npcMarketList;
        }

        public RoMapEntryDrainEvent(RoZcNpcMarketPurchaseResult npcMarketResult) : this(RoMapEntryDrainEventKind.NpcMarketResult) {
            payload = npcMarketResult;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcNpcBarterList npcBarterList) : this(kind) {
            NpcBarterList = npcBarterList;
        }

        public RoMapEntryDrainEvent(RoZcStorageAmount storageAmount) : this(RoMapEntryDrainEventKind.StorageAmount) {
            payload = storageAmount;
        }

        public RoMapEntryDrainEvent(RoZcStorageItem storageItem) : this(RoMapEntryDrainEventKind.StorageItemAdd) {
            StorageItem = storageItem;
        }

        public RoMapEntryDrainEvent(RoZcStorageItemDelete storageItemDelete) : this(RoMapEntryDrainEventKind.StorageItemDelete) {
            StorageItemDelete = storageItemDelete;
        }

        public RoMapEntryDrainEvent(RoZcCartAmount cartAmount) : this(RoMapEntryDrainEventKind.CartAmount) {
            payload = cartAmount;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcStorageItem cartItem) : this(kind) {
            StorageItem = cartItem;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcStorageItemDelete cartItemDelete) : this(kind) {
            StorageItemDelete = cartItemDelete;
        }

        public static RoMapEntryDrainEvent CartOff() {
            return new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.CartOff);
        }

        public RoMapEntryDrainEvent(RoZcBankCheck bankCheck) : this(RoMapEntryDrainEventKind.BankCheck) {
            payload = bankCheck;
        }

        public RoMapEntryDrainEvent(RoZcRodexMailList rodexList) : this(RoMapEntryDrainEventKind.RodexList) {
            payload = rodexList;
        }

        public static RoMapEntryDrainEvent RodexFailedList(bool openWindow = true) {
            return new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.RodexListFailed) {
                RodexListFailureOpensWindow = openWindow
            };
        }

        public RoMapEntryDrainEvent(RoZcRodexReadMail rodexRead) : this(RoMapEntryDrainEventKind.RodexRead) {
            payload = rodexRead;
        }

        public RoMapEntryDrainEvent(RoZcRodexSendAck rodexSendAck) : this(RoMapEntryDrainEventKind.RodexSendAck) {
            payload = rodexSendAck;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcRodexAttachmentAck rodexAttachmentAck) : this(kind) {
            RodexAttachmentAck = rodexAttachmentAck;
        }

        public RoMapEntryDrainEvent(RoZcRodexDeleteAck rodexDeleteAck) : this(RoMapEntryDrainEventKind.RodexDeleteAck) {
            payload = rodexDeleteAck;
        }

        public RoMapEntryDrainEvent(RoZcRodexAddItemAck rodexAddItemAck) : this(RoMapEntryDrainEventKind.RodexAddItemAck) {
            payload = rodexAddItemAck;
        }

        public RoMapEntryDrainEvent(RoZcRodexRemoveItemAck rodexRemoveItemAck) : this(RoMapEntryDrainEventKind.RodexRemoveItemAck) {
            payload = rodexRemoveItemAck;
        }

        public RoMapEntryDrainEvent(RoZcRodexOpenWriteAck rodexOpenWriteAck) : this(RoMapEntryDrainEventKind.RodexOpenWriteAck) {
            payload = rodexOpenWriteAck;
        }

        public RoMapEntryDrainEvent(RoZcRodexCheckNameAck rodexCheckNameAck) : this(RoMapEntryDrainEventKind.RodexCheckNameAck) {
            payload = rodexCheckNameAck;
        }

        public RoMapEntryDrainEvent(RoZcNotifyUnreadMail unreadMail) : this(RoMapEntryDrainEventKind.UnreadMail) {
            payload = unreadMail;
        }

        public RoMapEntryDrainEvent(RoZcChangeItemOption changeItemOption) : this(RoMapEntryDrainEventKind.ChangeItemOption) {
            payload = changeItemOption;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcBankTransactionAck bankTransactionAck) : this(kind) {
            BankTransactionAck = bankTransactionAck;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcBankSimpleAck bankSimpleAck) : this(kind) {
            BankSimpleAck = bankSimpleAck;
        }

        public RoMapEntryDrainEvent(RoZcTradeRequest tradeRequest) : this(RoMapEntryDrainEventKind.TradeRequest) {
            payload = tradeRequest;
        }

        public RoMapEntryDrainEvent(RoZcTradeAck tradeAck) : this(RoMapEntryDrainEventKind.TradeAck) {
            payload = tradeAck;
        }

        public RoMapEntryDrainEvent(RoZcTradeItem tradeItem) : this(RoMapEntryDrainEventKind.TradeItemAdd) {
            payload = tradeItem;
        }

        public RoMapEntryDrainEvent(RoZcTradeAddItemAck tradeAddItemAck) : this(RoMapEntryDrainEventKind.TradeAddItemAck) {
            payload = tradeAddItemAck;
        }

        public RoMapEntryDrainEvent(RoZcTradeConclude tradeConclude) : this(RoMapEntryDrainEventKind.TradeConclude) {
            payload = tradeConclude;
        }

        public static RoMapEntryDrainEvent TradeCancel() {
            return new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.TradeCancel);
        }

        public RoMapEntryDrainEvent(RoZcTradeSubmitResult tradeSubmit) : this(RoMapEntryDrainEventKind.TradeSubmit) {
            payload = tradeSubmit;
        }

        public RoMapEntryDrainEvent(RoZcGuildBelongInfo guildBelongInfo) : this(RoMapEntryDrainEventKind.GuildBelongInfo) {
            payload = guildBelongInfo;
        }

        public RoMapEntryDrainEvent(RoZcGuildNotice guildNotice) : this(RoMapEntryDrainEventKind.GuildNotice) {
            payload = guildNotice;
        }

        public RoMapEntryDrainEvent(RoZcGuildMemberInfo guildMemberInfo) : this(RoMapEntryDrainEventKind.GuildMemberInfo) {
            payload = guildMemberInfo;
        }

        public RoMapEntryDrainEvent(RoZcGuildInvite guildInvite) : this(RoMapEntryDrainEventKind.GuildInvite) {
            payload = guildInvite;
        }

        public RoMapEntryDrainEvent(RoZcGuildInviteResult guildInviteResult) : this(RoMapEntryDrainEventKind.GuildInviteResult) {
            payload = guildInviteResult;
        }

        public RoMapEntryDrainEvent(RoZcGuildLeaveResult guildLeaveResult) : this(RoMapEntryDrainEventKind.GuildLeaveResult) {
            payload = guildLeaveResult;
        }

        public RoMapEntryDrainEvent(RoZcGuildChat guildChat) : this(RoMapEntryDrainEventKind.GuildChat) {
            payload = guildChat;
        }

        public RoMapEntryDrainEvent(RoZcPartyChat partyChat) : this(RoMapEntryDrainEventKind.PartyChat) {
            payload = partyChat;
        }

        public RoMapEntryDrainEvent(RoZcClanChat clanChat) : this(RoMapEntryDrainEventKind.ClanChat) {
            payload = clanChat;
        }

        public RoMapEntryDrainEvent(RoZcPetProperty petProperty) : this(RoMapEntryDrainEventKind.PetProperty) {
            payload = petProperty;
        }

        public RoMapEntryDrainEvent(RoZcPetFeedResult petFeedResult) : this(RoMapEntryDrainEventKind.PetFeedResult) {
            payload = petFeedResult;
        }

        public RoMapEntryDrainEvent(RoZcPetChangeState petChangeState) : this(RoMapEntryDrainEventKind.PetChangeState) {
            payload = petChangeState;
        }

        public RoMapEntryDrainEvent(RoZcPetAction petAction) : this(RoMapEntryDrainEventKind.PetAction) {
            payload = petAction;
        }

        public RoMapEntryDrainEvent(RoZcHomunculusProperty homunculusProperty) : this(RoMapEntryDrainEventKind.HomunculusProperty) {
            payload = homunculusProperty;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcCompanionChangeState companionChangeState) : this(kind) {
            CompanionChangeState = companionChangeState;
        }

        public RoMapEntryDrainEvent(RoZcCompanionParameterChange companionParameterChange) : this(RoMapEntryDrainEventKind.HomunculusParameterChange) {
            payload = companionParameterChange;
        }

        public RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind, RoZcSkillInfoList companionSkillInfoList) : this(kind) {
            CompanionSkillInfoList = companionSkillInfoList;
        }

        public static RoMapEntryDrainEvent GuildManagerEvent(RoGuildUpdate update) =>
            new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.GuildManager) { GuildUpdate = update };

        public static RoMapEntryDrainEvent HomunculusSkill(RoZcSkillInfo skill) =>
            new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.HomunculusSkillUpdate) { HomunculusSkillUpdate = skill };

        public static RoMapEntryDrainEvent MercenaryParameterEvent(RoZcCompanionParameterChange change) =>
            new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.MercenaryParameterChange) { MercenaryParameter = change };

        public RoMapEntryDrainEvent(RoZcMercenaryInfo mercenaryInfo) : this(RoMapEntryDrainEventKind.MercenaryInfo) {
            payload = mercenaryInfo;
        }

        public RoMapEntryDrainEvent(RoZcMapProperty mapProperty) : this(RoMapEntryDrainEventKind.MapProperty) {
            payload = mapProperty;
        }

        public RoMapEntryDrainEvent(RoZcNotifyRanking ranking) : this(RoMapEntryDrainEventKind.PvpRanking) {
            payload = ranking;
        }

        public RoMapEntryDrainEvent(RoZcBattlefieldScore battlefieldScore) : this(RoMapEntryDrainEventKind.BattlefieldScore) {
            payload = battlefieldScore;
        }

        public RoMapEntryDrainEvent(RoZcBattlefieldCampInfo campInfo) : this(RoMapEntryDrainEventKind.BattlefieldCampInfo) {
            payload = campInfo;
        }

        public RoMapEntryDrainEvent(RoZcBattlefieldPosition position) : this(RoMapEntryDrainEventKind.BattlefieldPosition) {
            payload = position;
        }

        public RoMapEntryDrainEvent(RoZcBattlefieldHp hp) : this(RoMapEntryDrainEventKind.BattlefieldHp) {
            payload = hp;
        }

        public RoMapEntryDrainEvent(RoZcNpcMapMove npcMapMove) : this(RoMapEntryDrainEventKind.NpcMapMove) {
            payload = npcMapMove;
        }

        public RoMapEntryDrainEvent(RoZcNpcServerMove npcServerMove) : this(RoMapEntryDrainEventKind.NpcServerMove) {
            payload = npcServerMove;
        }

        public RoMapEntryDrainEvent(RoZcNpcCutin npcCutin) : this(RoMapEntryDrainEventKind.NpcCutin) {
            payload = npcCutin;
        }

        public RoMapEntryDrainEvent(RoZcShowScript showScript) : this(RoMapEntryDrainEventKind.NpcShowScript) {
            payload = showScript;
        }

        public RoMapEntryDrainEvent(RoZcCompass minimapMarker) : this(RoMapEntryDrainEventKind.MinimapMarker) {
            payload = minimapMarker;
        }

        public RoMapEntryDrainEvent(RoZcUiOpen uiOpen) : this(RoMapEntryDrainEventKind.UiOpen) {
            payload = uiOpen;
        }

        public RoMapEntryDrainEvent(RoZcAttendanceResponse attendanceResponse) : this(RoMapEntryDrainEventKind.AttendanceResponse) {
            payload = attendanceResponse;
        }

        public RoMapEntryDrainEvent(RoZcAckToUseSkill skillUseAck) : this(RoMapEntryDrainEventKind.SkillUseAck) {
            payload = skillUseAck;
        }

        public RoMapEntryDrainEvent(RoZcAttackFailureForDistance attackFailureForDistance) : this(RoMapEntryDrainEventKind.AttackFailureForDistance) {
            payload = attackFailureForDistance;
        }

        public RoMapEntryDrainEvent(RoZcActionFailure actionFailure) : this(RoMapEntryDrainEventKind.ActionFailure) {
            payload = actionFailure;
        }

        public RoMapEntryDrainEvent(RoZcUseSkillAck skillCast) : this(RoMapEntryDrainEventKind.SkillCast) {
            payload = skillCast;
        }

        public RoMapEntryDrainEvent(RoZcNotifySkill skillDamage) : this(RoMapEntryDrainEventKind.SkillDamage) {
            payload = skillDamage;
        }

        public RoMapEntryDrainEvent(RoZcUseSkillResult skillResult) : this(RoMapEntryDrainEventKind.SkillResult) {
            payload = skillResult;
        }

        public RoMapEntryDrainEvent(RoZcNotifyEffect notifyEffect) : this(RoMapEntryDrainEventKind.NotifyEffect) {
            payload = notifyEffect;
        }

        public RoMapEntryDrainEvent(RoZcNotifyGroundSkill groundSkill) : this(RoMapEntryDrainEventKind.GroundSkill) {
            payload = groundSkill;
        }

        public RoMapEntryDrainEvent(RoZcSkillPostDelay skillPostDelay) : this(RoMapEntryDrainEventKind.SkillPostDelay) {
            payload = skillPostDelay;
        }

        public RoMapEntryDrainEvent(RoZcNotifySkillPosition skillPosition) : this(RoMapEntryDrainEventKind.SkillPosition) {
            payload = skillPosition;
        }

        public RoMapEntryDrainEvent(RoZcSkillEntry skillEntry) : this(RoMapEntryDrainEventKind.SkillEntry) {
            payload = skillEntry;
        }

        public RoMapEntryDrainEvent(RoZcSkillDisappear skillDisappear) : this(RoMapEntryDrainEventKind.SkillDisappear) {
            payload = skillDisappear;
        }

        public static RoMapEntryDrainEvent StorageClosed() {
            return new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.StorageClose);
        }

        public static RoMapEntryDrainEvent VendingClosed() {
            return new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.VendingClosed);
        }

        private RoMapEntryDrainEvent(RoMapEntryDrainEventKind kind) {
            Kind = kind;
        }

        public RoMapEntryDrainEventKind Kind { get; }

        // the packet of the event's kind, boxed once: the single-packet properties below read it back for their kind
        // (default otherwise, as when each kind had its own field). With ~185 inline struct fields an event weighed
        // ~3 KB, and the drain makes one per packet: 60 KB a frame for a walking crowd of 250.
        private readonly object payload;
        public RoZcNotifyPlayerChat PlayerChat => Kind == RoMapEntryDrainEventKind.PlayerChat ? (RoZcNotifyPlayerChat) payload : default;
        public RoZcNpcChat NpcChat => Kind == RoMapEntryDrainEventKind.NpcChat ? (RoZcNpcChat) payload : default;
        public RoZcSystemText SystemText => Kind == RoMapEntryDrainEventKind.SystemText ? (RoZcSystemText) payload : default;
        public RoZcWhisper Whisper => Kind == RoMapEntryDrainEventKind.Whisper ? (RoZcWhisper) payload : default;
        public RoZcWhisperAck WhisperAck => Kind == RoMapEntryDrainEventKind.WhisperAck ? (RoZcWhisperAck) payload : default;
        public RoZcMessage Message => Kind == RoMapEntryDrainEventKind.Message ? (RoZcMessage) payload : default;
        public RoZcNotifyTime TimeNotification => Kind == RoMapEntryDrainEventKind.TimeNotification ? (RoZcNotifyTime) payload : default;
        public RoZcNotifyActorEntry ActorEntry => Kind == RoMapEntryDrainEventKind.ActorEntry ? (RoZcNotifyActorEntry) payload : default;
        public RoZcNotifyMove ActorMove => Kind == RoMapEntryDrainEventKind.ActorMove ? (RoZcNotifyMove) payload : default;
        public RoZcNotifyPlayerMove PlayerMove => Kind == RoMapEntryDrainEventKind.PlayerMove ? (RoZcNotifyPlayerMove) payload : default;
        public RoZcStopMove StopMove => Kind == RoMapEntryDrainEventKind.StopMove ? (RoZcStopMove) payload : default;
        public RoZcActorIdentity ActorIdentity => Kind == RoMapEntryDrainEventKind.ActorIdentity ? (RoZcActorIdentity) payload : default;
        public RoZcHpInfo ActorHpInfo => Kind == RoMapEntryDrainEventKind.ActorHpInfo ? (RoZcHpInfo) payload : default;
        public RoZcNotifyVanish ActorVanish => Kind == RoMapEntryDrainEventKind.ActorVanish ? (RoZcNotifyVanish) payload : default;
        public RoZcResurrection ActorResurrection => Kind == RoMapEntryDrainEventKind.ActorResurrection ? (RoZcResurrection) payload : default;
        public RoZcChangeDirection ActorDirectionChange => Kind == RoMapEntryDrainEventKind.ActorDirectionChange ? (RoZcChangeDirection) payload : default;
        public RoZcSpriteChange ActorSpriteChange => Kind == RoMapEntryDrainEventKind.ActorSpriteChange ? (RoZcSpriteChange) payload : default;
        public RoZcItemEntry GroundItemEntry => Kind == RoMapEntryDrainEventKind.GroundItemEntry ? (RoZcItemEntry) payload : default;
        public RoZcItemDisappear GroundItemDisappear => Kind == RoMapEntryDrainEventKind.GroundItemDisappear ? (RoZcItemDisappear) payload : default;
        public RoZcNotifyAct ActorAction => Kind == RoMapEntryDrainEventKind.ActorAction ? (RoZcNotifyAct) payload : default;
        public RoZcEmotion Emotion => Kind == RoMapEntryDrainEventKind.Emotion ? (RoZcEmotion) payload : default;
        public RoZcMsgStateChange StatusChange => Kind == RoMapEntryDrainEventKind.StatusChange ? (RoZcMsgStateChange) payload : default;
        public RoZcStateChange ActorStateChange => Kind == RoMapEntryDrainEventKind.ActorStateChange ? (RoZcStateChange) payload : default;
        public RoZcInventoryStart InventoryStart => Kind == RoMapEntryDrainEventKind.InventoryStart ? (RoZcInventoryStart) payload : default;
        public RoZcInventoryItemList InventoryList => Kind == RoMapEntryDrainEventKind.InventoryList ? (RoZcInventoryItemList) payload : default;
        public RoZcInventoryEnd InventoryEnd => Kind == RoMapEntryDrainEventKind.InventoryEnd ? (RoZcInventoryEnd) payload : default;
        public RoZcEquipSwitchList EquipSwitchList => Kind == RoMapEntryDrainEventKind.EquipSwitchList ? (RoZcEquipSwitchList) payload : default;
        public RoZcEquipWinMicroscope PlayerViewEquipment => Kind == RoMapEntryDrainEventKind.PlayerViewEquipment ? (RoZcEquipWinMicroscope) payload : default;
        public RoZcNavigationActive NavigationActive => Kind == RoMapEntryDrainEventKind.NavigationActive ? (RoZcNavigationActive) payload : default;
        public RoZcBattlefieldChat BattlefieldChat => Kind == RoMapEntryDrainEventKind.BattlefieldChat ? (RoZcBattlefieldChat) payload : default;
        public RoZcEntryQueue EntryQueue => Kind == RoMapEntryDrainEventKind.EntryQueue ? (RoZcEntryQueue) payload : default;
        public RoZcParameterChange ParameterChange => Kind == RoMapEntryDrainEventKind.ParameterChange ? (RoZcParameterChange) payload : default;
        public RoZcStatusSnapshot StatusSnapshot => Kind == RoMapEntryDrainEventKind.StatusSnapshot ? (RoZcStatusSnapshot) payload : default;
        public RoZcStatusPropertyChange StatusPropertyChange => Kind == RoMapEntryDrainEventKind.StatusPropertyChange ? (RoZcStatusPropertyChange) payload : default;
        public RoZcStatusChangeAck StatusChangeAck => Kind == RoMapEntryDrainEventKind.StatusChangeAck ? (RoZcStatusChangeAck) payload : default;
        public RoZcCoupleStatus StatusBaseBonus => Kind == RoMapEntryDrainEventKind.StatusBaseBonus ? (RoZcCoupleStatus) payload : default;
        public RoZcNotifyWeightPercentage WeightPercentage => Kind == RoMapEntryDrainEventKind.WeightPercentage ? (RoZcNotifyWeightPercentage) payload : default;
        public RoZcAttackRange AttackRange => Kind == RoMapEntryDrainEventKind.AttackRange ? (RoZcAttackRange) payload : default;
        public RoZcDispel CastCancel => Kind == RoMapEntryDrainEventKind.CastCancel ? (RoZcDispel) payload : default;
        public RoZcSpirits ActorSpirits => Kind == RoMapEntryDrainEventKind.ActorSpirits ? (RoZcSpirits) payload : default;
        public RoZcNotifyEffectById NotifyEffectById => Kind == RoMapEntryDrainEventKind.NotifyEffectById ? (RoZcNotifyEffectById) payload : default;
        public RoZcOptionChange2 OptionChange2 => Kind == RoMapEntryDrainEventKind.OptionChange2 ? (RoZcOptionChange2) payload : default;
        public RoZcSkillInfoList SkillInfoList => Kind == RoMapEntryDrainEventKind.SkillInfoList ? (RoZcSkillInfoList) payload : default;
        public RoZcSkillInfo SkillInfo { get; }
        public RoZcShortcutKeyList ShortcutKeyList => Kind == RoMapEntryDrainEventKind.ShortcutKeyList ? (RoZcShortcutKeyList) payload : default;
        public RoZcWearEquipmentAck WearEquipmentAck => Kind == RoMapEntryDrainEventKind.WearEquipmentAck ? (RoZcWearEquipmentAck) payload : default;
        public RoZcTakeoffEquipmentAck TakeoffEquipmentAck => Kind == RoMapEntryDrainEventKind.TakeoffEquipmentAck ? (RoZcTakeoffEquipmentAck) payload : default;
        public RoZcEquipArrow EquipArrow => Kind == RoMapEntryDrainEventKind.EquipArrow ? (RoZcEquipArrow) payload : default;
        public RoZcItemThrowAck ItemThrowAck => Kind == RoMapEntryDrainEventKind.ItemThrowAck ? (RoZcItemThrowAck) payload : default;
        public RoZcItemPickupAck ItemPickupAck => Kind == RoMapEntryDrainEventKind.ItemPickupAck ? (RoZcItemPickupAck) payload : default;
        public RoZcInventoryItemDelete InventoryItemDelete => Kind == RoMapEntryDrainEventKind.InventoryItemDelete ? (RoZcInventoryItemDelete) payload : default;
        public RoZcUseItemAck UseItemAck => Kind == RoMapEntryDrainEventKind.UseItemAck ? (RoZcUseItemAck) payload : default;
        public RoZcItemCompositionList ItemCompositionList => Kind == RoMapEntryDrainEventKind.ItemCompositionList ? (RoZcItemCompositionList) payload : default;
        public RoZcItemCompositionAck ItemCompositionAck => Kind == RoMapEntryDrainEventKind.ItemCompositionAck ? (RoZcItemCompositionAck) payload : default;
        public RoZcItemIdentifyList ItemIdentifyList => Kind == RoMapEntryDrainEventKind.ItemIdentifyList ? (RoZcItemIdentifyList) payload : default;
        public RoZcItemIdentifyAck ItemIdentifyAck => Kind == RoMapEntryDrainEventKind.ItemIdentifyAck ? (RoZcItemIdentifyAck) payload : default;
        public RoZcMakingArrowList MakingArrowList => Kind == RoMapEntryDrainEventKind.MakingArrowList ? (RoZcMakingArrowList) payload : default;
        public RoZcSelectCart SelectCart => Kind == RoMapEntryDrainEventKind.SelectCart ? (RoZcSelectCart) payload : default;
        public RoZcWarpList TeleportWarpList => Kind == RoMapEntryDrainEventKind.TeleportWarpList ? (RoZcWarpList) payload : default;
        public RoZcNotifyMapInfo TeleportResult => Kind == RoMapEntryDrainEventKind.TeleportResult ? (RoZcNotifyMapInfo) payload : default;
        public RoZcNpcDialogText NpcDialogText { get; }
        public RoZcNpcDialogSignal NpcDialogSignal { get; }
        public RoZcSelectDealType NpcShopDealType => Kind == RoMapEntryDrainEventKind.NpcShopDealType ? (RoZcSelectDealType) payload : default;
        public RoZcNpcShopBuyList NpcShopBuyList => Kind == RoMapEntryDrainEventKind.NpcShopBuyList ? (RoZcNpcShopBuyList) payload : default;
        public RoZcNpcShopSellList NpcShopSellList => Kind == RoMapEntryDrainEventKind.NpcShopSellList ? (RoZcNpcShopSellList) payload : default;
        public RoZcNpcShopResult NpcShopResult { get; }
        public RoZcOpenStore VendingOpenStore => Kind == RoMapEntryDrainEventKind.VendingSetupAllowed ? (RoZcOpenStore) payload : default;
        public RoZcStoreEntry VendingStoreEntry => Kind == RoMapEntryDrainEventKind.VendingBoardAdded ? (RoZcStoreEntry) payload : default;
        public RoZcDisappearEntry VendingDisappearEntry => Kind == RoMapEntryDrainEventKind.VendingBoardRemoved ? (RoZcDisappearEntry) payload : default;
        public RoZcPlayerVendingItemList VendingItemList { get; }
        public RoZcPlayerVendingPurchaseResult VendingPurchaseResult => Kind == RoMapEntryDrainEventKind.PlayerShopPurchaseResult ? (RoZcPlayerVendingPurchaseResult) payload : default;
        public RoZcDeleteItemFromMcStore VendingSaleReport => Kind == RoMapEntryDrainEventKind.VendingSaleReported ? (RoZcDeleteItemFromMcStore) payload : default;
        public RoZcAckOpenStore2 VendingOpenAck => Kind == RoMapEntryDrainEventKind.VendingOpenAck ? (RoZcAckOpenStore2) payload : default;
        public RoZcNpcMarketList NpcMarketList => Kind == RoMapEntryDrainEventKind.NpcMarketList ? (RoZcNpcMarketList) payload : default;
        public RoZcNpcMarketPurchaseResult NpcMarketResult => Kind == RoMapEntryDrainEventKind.NpcMarketResult ? (RoZcNpcMarketPurchaseResult) payload : default;
        public RoZcNpcBarterList NpcBarterList { get; }
        public RoZcStorageAmount StorageAmount => Kind == RoMapEntryDrainEventKind.StorageAmount ? (RoZcStorageAmount) payload : default;
        public RoZcStorageItem StorageItem { get; }
        public RoZcStorageItemDelete StorageItemDelete { get; }
        public RoZcCartAmount CartAmount => Kind == RoMapEntryDrainEventKind.CartAmount ? (RoZcCartAmount) payload : default;
        public RoZcBankCheck BankCheck => Kind == RoMapEntryDrainEventKind.BankCheck ? (RoZcBankCheck) payload : default;
        public RoZcBankTransactionAck BankTransactionAck { get; }
        public RoZcBankSimpleAck BankSimpleAck { get; }
        public RoZcRodexMailList RodexList => Kind == RoMapEntryDrainEventKind.RodexList ? (RoZcRodexMailList) payload : default;
        public bool RodexListFailureOpensWindow { get; private set; }
        public RoZcRodexReadMail RodexRead => Kind == RoMapEntryDrainEventKind.RodexRead ? (RoZcRodexReadMail) payload : default;
        public RoZcRodexSendAck RodexSendAck => Kind == RoMapEntryDrainEventKind.RodexSendAck ? (RoZcRodexSendAck) payload : default;
        public RoZcRodexAttachmentAck RodexAttachmentAck { get; }
        public RoZcRodexDeleteAck RodexDeleteAck => Kind == RoMapEntryDrainEventKind.RodexDeleteAck ? (RoZcRodexDeleteAck) payload : default;
        public RoZcRodexAddItemAck RodexAddItemAck => Kind == RoMapEntryDrainEventKind.RodexAddItemAck ? (RoZcRodexAddItemAck) payload : default;
        public RoZcRodexRemoveItemAck RodexRemoveItemAck => Kind == RoMapEntryDrainEventKind.RodexRemoveItemAck ? (RoZcRodexRemoveItemAck) payload : default;
        public RoZcRodexOpenWriteAck RodexOpenWriteAck => Kind == RoMapEntryDrainEventKind.RodexOpenWriteAck ? (RoZcRodexOpenWriteAck) payload : default;
        public RoZcRodexCheckNameAck RodexCheckNameAck => Kind == RoMapEntryDrainEventKind.RodexCheckNameAck ? (RoZcRodexCheckNameAck) payload : default;
        public RoZcNotifyUnreadMail UnreadMail => Kind == RoMapEntryDrainEventKind.UnreadMail ? (RoZcNotifyUnreadMail) payload : default;
        public RoZcChangeItemOption ChangeItemOption => Kind == RoMapEntryDrainEventKind.ChangeItemOption ? (RoZcChangeItemOption) payload : default;
        public RoZcTradeRequest TradeRequest => Kind == RoMapEntryDrainEventKind.TradeRequest ? (RoZcTradeRequest) payload : default;
        public RoZcTradeAck TradeAck => Kind == RoMapEntryDrainEventKind.TradeAck ? (RoZcTradeAck) payload : default;
        public RoZcTradeItem TradeItem => Kind == RoMapEntryDrainEventKind.TradeItemAdd ? (RoZcTradeItem) payload : default;
        public RoZcTradeAddItemAck TradeAddItemAck => Kind == RoMapEntryDrainEventKind.TradeAddItemAck ? (RoZcTradeAddItemAck) payload : default;
        public RoZcTradeConclude TradeConclude => Kind == RoMapEntryDrainEventKind.TradeConclude ? (RoZcTradeConclude) payload : default;
        public RoZcTradeSubmitResult TradeSubmit => Kind == RoMapEntryDrainEventKind.TradeSubmit ? (RoZcTradeSubmitResult) payload : default;
        public RoZcNpcMapMove NpcMapMove => Kind == RoMapEntryDrainEventKind.NpcMapMove ? (RoZcNpcMapMove) payload : default;
        public RoZcNpcServerMove NpcServerMove => Kind == RoMapEntryDrainEventKind.NpcServerMove ? (RoZcNpcServerMove) payload : default;
        public RoZcNpcCutin NpcCutin => Kind == RoMapEntryDrainEventKind.NpcCutin ? (RoZcNpcCutin) payload : default;
        public RoZcShowScript ShowScript => Kind == RoMapEntryDrainEventKind.NpcShowScript ? (RoZcShowScript) payload : default;
        public RoZcAckToUseSkill SkillUseAck => Kind == RoMapEntryDrainEventKind.SkillUseAck ? (RoZcAckToUseSkill) payload : default;
        public RoZcAttackFailureForDistance AttackFailureForDistance => Kind == RoMapEntryDrainEventKind.AttackFailureForDistance ? (RoZcAttackFailureForDistance) payload : default;
        public RoZcActionFailure ActionFailure => Kind == RoMapEntryDrainEventKind.ActionFailure ? (RoZcActionFailure) payload : default;
        public RoZcUseSkillAck SkillCast => Kind == RoMapEntryDrainEventKind.SkillCast ? (RoZcUseSkillAck) payload : default;
        public RoZcNotifySkill SkillDamage => Kind == RoMapEntryDrainEventKind.SkillDamage ? (RoZcNotifySkill) payload : default;
        public RoZcUseSkillResult SkillResult => Kind == RoMapEntryDrainEventKind.SkillResult ? (RoZcUseSkillResult) payload : default;
        public RoZcNotifyEffect NotifyEffect => Kind == RoMapEntryDrainEventKind.NotifyEffect ? (RoZcNotifyEffect) payload : default;
        public RoZcNotifyGroundSkill GroundSkill => Kind == RoMapEntryDrainEventKind.GroundSkill ? (RoZcNotifyGroundSkill) payload : default;
        public RoZcSkillPostDelay SkillPostDelay => Kind == RoMapEntryDrainEventKind.SkillPostDelay ? (RoZcSkillPostDelay) payload : default;
        public RoZcNotifySkillPosition SkillPosition => Kind == RoMapEntryDrainEventKind.SkillPosition ? (RoZcNotifySkillPosition) payload : default;
        public RoZcSkillEntry SkillEntry => Kind == RoMapEntryDrainEventKind.SkillEntry ? (RoZcSkillEntry) payload : default;
        public RoZcSkillDisappear SkillDisappear => Kind == RoMapEntryDrainEventKind.SkillDisappear ? (RoZcSkillDisappear) payload : default;
        public RoZcQuestList QuestList => Kind == RoMapEntryDrainEventKind.QuestList ? (RoZcQuestList) payload : default;
        public RoZcQuestAdd QuestAdd => Kind == RoMapEntryDrainEventKind.QuestAdd ? (RoZcQuestAdd) payload : default;
        public RoZcQuestDelete QuestDelete => Kind == RoMapEntryDrainEventKind.QuestDelete ? (RoZcQuestDelete) payload : default;
        public RoZcQuestActive QuestActive => Kind == RoMapEntryDrainEventKind.QuestActive ? (RoZcQuestActive) payload : default;
        public RoZcQuestObjectiveUpdate QuestObjectiveUpdate => Kind == RoMapEntryDrainEventKind.QuestObjectiveUpdate ? (RoZcQuestObjectiveUpdate) payload : default;
        public RoZcPartyOption PartyOption => Kind == RoMapEntryDrainEventKind.PartyOption ? (RoZcPartyOption) payload : default;
        public RoZcPartyCreateAck PartyCreateAck => Kind == RoMapEntryDrainEventKind.PartyCreateAck ? (RoZcPartyCreateAck) payload : default;
        public RoZcPartyConfig PartyConfig => Kind == RoMapEntryDrainEventKind.PartyConfig ? (RoZcPartyConfig) payload : default;
        public RoZcPartyLeaderChanged PartyLeaderChanged => Kind == RoMapEntryDrainEventKind.PartyLeaderChanged ? (RoZcPartyLeaderChanged) payload : default;
        public RoZcPartyInfo PartyInfo => Kind == RoMapEntryDrainEventKind.PartyInfo ? (RoZcPartyInfo) payload : default;
        public RoZcPartyMemberInfo PartyMemberInfo => Kind == RoMapEntryDrainEventKind.PartyMemberInfo ? (RoZcPartyMemberInfo) payload : default;
        public RoZcPartyMemberLeave PartyMemberLeave => Kind == RoMapEntryDrainEventKind.PartyMemberLeave ? (RoZcPartyMemberLeave) payload : default;
        public RoZcPartyPosition PartyPosition => Kind == RoMapEntryDrainEventKind.PartyPosition ? (RoZcPartyPosition) payload : default;
        public RoZcPartyHp PartyHp => Kind == RoMapEntryDrainEventKind.PartyHp ? (RoZcPartyHp) payload : default;
        public RoZcPartyAlive PartyAlive => Kind == RoMapEntryDrainEventKind.PartyAlive ? (RoZcPartyAlive) payload : default;
        public RoZcPartyJobAndLevel PartyJobAndLevel => Kind == RoMapEntryDrainEventKind.PartyJobAndLevel ? (RoZcPartyJobAndLevel) payload : default;
        public RoZcPartyInvite PartyInvite => Kind == RoMapEntryDrainEventKind.PartyInvite ? (RoZcPartyInvite) payload : default;
        public RoZcPartyInviteAck PartyInviteAck => Kind == RoMapEntryDrainEventKind.PartyInviteAck ? (RoZcPartyInviteAck) payload : default;
        public RoZcFriendsList FriendsList => Kind == RoMapEntryDrainEventKind.FriendsList ? (RoZcFriendsList) payload : default;
        public RoZcFriendState FriendState => Kind == RoMapEntryDrainEventKind.FriendState ? (RoZcFriendState) payload : default;
        public RoZcFriendRequest FriendRequest => Kind == RoMapEntryDrainEventKind.FriendRequest ? (RoZcFriendRequest) payload : default;
        public RoZcFriendAddResult FriendAddResult => Kind == RoMapEntryDrainEventKind.FriendAddResult ? (RoZcFriendAddResult) payload : default;
        public RoZcFriendDelete FriendDelete => Kind == RoMapEntryDrainEventKind.FriendDelete ? (RoZcFriendDelete) payload : default;
        public RoZcAckReqNameByGid CharacterNameByGid => Kind == RoMapEntryDrainEventKind.CharacterNameByGid ? (RoZcAckReqNameByGid) payload : default;
        public RoZcChatRoomResult ChatRoomResult { get; }
        public RoChatRoomInfo ChatRoomInfo => Kind == RoMapEntryDrainEventKind.ChatRoomInfo ? (RoChatRoomInfo) payload : default;
        public uint DestroyedChatId => Kind == RoMapEntryDrainEventKind.ChatRoomDestroy ? (uint) payload : default;
        public RoZcChatRoomEnter ChatRoomEnter => Kind == RoMapEntryDrainEventKind.ChatRoomEnter ? (RoZcChatRoomEnter) payload : default;
        public RoZcChatRoomMemberChange ChatRoomMember { get; }
        public RoZcChatRoomRole ChatRoomRole => Kind == RoMapEntryDrainEventKind.ChatRoomRole ? (RoZcChatRoomRole) payload : default;
        public RoZcBuyingStoreResult BuyingStoreResult { get; }
        public RoZcBuyingStoreList BuyingStoreList { get; }
        public RoZcBuyingStoreEntry BuyingStoreEntry { get; }
        public RoZcBuyingStoreSold BuyingStoreSold => Kind == RoMapEntryDrainEventKind.BuyingStoreSold ? (RoZcBuyingStoreSold) payload : default;
        public RoZcBuyingStoreUpdate BuyingStoreUpdate => Kind == RoMapEntryDrainEventKind.BuyingStoreUpdate ? (RoZcBuyingStoreUpdate) payload : default;
        public RoZcRepairItemList RepairItemList => Kind == RoMapEntryDrainEventKind.RepairItemList ? (RoZcRepairItemList) payload : default;
        public RoZcAckItemRepair RepairItemResult => Kind == RoMapEntryDrainEventKind.RepairItemResult ? (RoZcAckItemRepair) payload : default;
        public RoZcRepairItemList WeaponRefineList { get; private set; }
        public RoZcAckWeaponRefine WeaponRefineResult => Kind == RoMapEntryDrainEventKind.WeaponRefineResult ? (RoZcAckWeaponRefine) payload : default;
        public RoZcAutoSpellList AutoSpellList => Kind == RoMapEntryDrainEventKind.AutoSpellList ? (RoZcAutoSpellList) payload : default;
        public RoZcMergeItemOpen MergeItemOpen => Kind == RoMapEntryDrainEventKind.MergeItemOpen ? (RoZcMergeItemOpen) payload : default;
        public RoZcAckMergeItem MergeItemResult => Kind == RoMapEntryDrainEventKind.MergeItemResult ? (RoZcAckMergeItem) payload : default;
        public RoZcMakableItemList MakableItemList => Kind == RoMapEntryDrainEventKind.MakableItemList ? (RoZcMakableItemList) payload : default;
        public RoZcPetEggList PetEggList => Kind == RoMapEntryDrainEventKind.PetEggList ? (RoZcPetEggList) payload : default;
        public byte PetCaptureResult { get; private set; }
        public uint PetEvolutionResult { get; private set; }
        /// <summary>ServerMapMark: 1 guild position, 2 quest NPC mark, 3 boss info, 4 ping.</summary>
        public byte MarkerKind { get; private set; }
        public RoZcGuildPosition GuildPosition { get; private set; }
        public RoZcQuestNotifyEffect QuestNotifyEffect { get; private set; }
        public RoZcBossInfo BossInfo { get; private set; }
        public RoZcMinimapPing MinimapPing { get; private set; }
        public RoZcAckReqMakingItem MakingItemResult => Kind == RoMapEntryDrainEventKind.MakingItemResult ? (RoZcAckReqMakingItem) payload : default;
        public RoZcGuildBelongInfo GuildBelongInfo => Kind == RoMapEntryDrainEventKind.GuildBelongInfo ? (RoZcGuildBelongInfo) payload : default;
        public RoZcGuildNotice GuildNotice => Kind == RoMapEntryDrainEventKind.GuildNotice ? (RoZcGuildNotice) payload : default;
        public RoZcGuildMemberInfo GuildMemberInfo => Kind == RoMapEntryDrainEventKind.GuildMemberInfo ? (RoZcGuildMemberInfo) payload : default;
        public RoZcGuildInvite GuildInvite => Kind == RoMapEntryDrainEventKind.GuildInvite ? (RoZcGuildInvite) payload : default;
        public RoZcGuildInviteResult GuildInviteResult => Kind == RoMapEntryDrainEventKind.GuildInviteResult ? (RoZcGuildInviteResult) payload : default;
        public RoZcGuildLeaveResult GuildLeaveResult => Kind == RoMapEntryDrainEventKind.GuildLeaveResult ? (RoZcGuildLeaveResult) payload : default;
        public RoZcGuildChat GuildChat => Kind == RoMapEntryDrainEventKind.GuildChat ? (RoZcGuildChat) payload : default;
        public RoZcPartyChat PartyChat => Kind == RoMapEntryDrainEventKind.PartyChat ? (RoZcPartyChat) payload : default;
        public RoZcClanChat ClanChat => Kind == RoMapEntryDrainEventKind.ClanChat ? (RoZcClanChat) payload : default;
        public RoZcPetProperty PetProperty => Kind == RoMapEntryDrainEventKind.PetProperty ? (RoZcPetProperty) payload : default;
        public RoZcPetFeedResult PetFeedResult => Kind == RoMapEntryDrainEventKind.PetFeedResult ? (RoZcPetFeedResult) payload : default;
        public RoZcPetChangeState PetChangeState => Kind == RoMapEntryDrainEventKind.PetChangeState ? (RoZcPetChangeState) payload : default;
        public RoZcPetAction PetAction => Kind == RoMapEntryDrainEventKind.PetAction ? (RoZcPetAction) payload : default;
        public RoZcHomunculusProperty HomunculusProperty => Kind == RoMapEntryDrainEventKind.HomunculusProperty ? (RoZcHomunculusProperty) payload : default;
        public RoZcCompanionChangeState CompanionChangeState { get; }
        public RoZcCompanionParameterChange CompanionParameterChange => Kind == RoMapEntryDrainEventKind.HomunculusParameterChange ? (RoZcCompanionParameterChange) payload : default;
        public RoZcSkillInfoList CompanionSkillInfoList { get; }
        public RoZcCompanionParameterChange MercenaryParameter { get; private set; }
        public RoZcSkillInfo HomunculusSkillUpdate { get; private set; }
        public RoGuildUpdate GuildUpdate { get; private set; }
        public RoZcMercenaryInfo MercenaryInfo => Kind == RoMapEntryDrainEventKind.MercenaryInfo ? (RoZcMercenaryInfo) payload : default;
        public RoZcMapProperty MapProperty => Kind == RoMapEntryDrainEventKind.MapProperty ? (RoZcMapProperty) payload : default;
        public RoZcNotifyRanking Ranking => Kind == RoMapEntryDrainEventKind.PvpRanking ? (RoZcNotifyRanking) payload : default;
        public RoZcBattlefieldScore BattlefieldScore => Kind == RoMapEntryDrainEventKind.BattlefieldScore ? (RoZcBattlefieldScore) payload : default;
        public RoZcBattlefieldCampInfo BattlefieldCampInfo => Kind == RoMapEntryDrainEventKind.BattlefieldCampInfo ? (RoZcBattlefieldCampInfo) payload : default;
        public RoZcBattlefieldPosition BattlefieldPosition => Kind == RoMapEntryDrainEventKind.BattlefieldPosition ? (RoZcBattlefieldPosition) payload : default;
        public RoZcBattlefieldHp BattlefieldHp => Kind == RoMapEntryDrainEventKind.BattlefieldHp ? (RoZcBattlefieldHp) payload : default;
        public RoZcCompass MinimapMarker => Kind == RoMapEntryDrainEventKind.MinimapMarker ? (RoZcCompass) payload : default;
        public RoZcUiOpen UiOpen => Kind == RoMapEntryDrainEventKind.UiOpen ? (RoZcUiOpen) payload : default;
        public RoZcAttendanceResponse AttendanceResponse => Kind == RoMapEntryDrainEventKind.AttendanceResponse ? (RoZcAttendanceResponse) payload : default;
    }

    public sealed class RoMapEntryDrainResult {
        public static readonly RoMapEntryDrainResult Empty = new RoMapEntryDrainResult(
            Array.Empty<RoMapEntryDrainedPacket>(),
            Array.Empty<RoZcNotifyPlayerChat>(),
            Array.Empty<RoZcNotifyTime>(),
            false,
            string.Empty);

        public RoMapEntryDrainResult(
            RoMapEntryDrainedPacket[] packets,
            RoZcNotifyPlayerChat[] playerChats,
            RoZcNotifyTime[] timeNotifications,
            bool stoppedOnUnsupportedPacket,
            string stopReason,
            int parseFailureCount = 0,
            string[] parseFailureMessages = null) : this(
            packets,
            playerChats,
            timeNotifications,
            Array.Empty<RoZcNotifyActorEntry>(),
            Array.Empty<RoZcNotifyMove>(),
            Array.Empty<RoZcNotifyPlayerMove>(),
            Array.Empty<RoZcStopMove>(),
            Array.Empty<RoZcNotifyVanish>(),
            stoppedOnUnsupportedPacket,
            stopReason,
            parseFailureCount,
            parseFailureMessages) {
        }

        public RoMapEntryDrainResult(
            RoMapEntryDrainedPacket[] packets,
            RoZcNotifyPlayerChat[] playerChats,
            RoZcNotifyTime[] timeNotifications,
            RoZcNotifyActorEntry[] actorEntries,
            bool stoppedOnUnsupportedPacket,
            string stopReason,
            int parseFailureCount = 0,
            string[] parseFailureMessages = null) {
            Packets = packets ?? Array.Empty<RoMapEntryDrainedPacket>();
            PlayerChats = playerChats ?? Array.Empty<RoZcNotifyPlayerChat>();
            TimeNotifications = timeNotifications ?? Array.Empty<RoZcNotifyTime>();
            ActorEntries = actorEntries ?? Array.Empty<RoZcNotifyActorEntry>();
            ActorMoves = Array.Empty<RoZcNotifyMove>();
            PlayerMoves = Array.Empty<RoZcNotifyPlayerMove>();
            StopMoves = Array.Empty<RoZcStopMove>();
            ActorVanishes = Array.Empty<RoZcNotifyVanish>();
            OrderedEvents = Array.Empty<RoMapEntryDrainEvent>();
            StoppedOnUnsupportedPacket = stoppedOnUnsupportedPacket;
            StopReason = stopReason ?? string.Empty;
            ParseFailureCount = Math.Max(0, parseFailureCount);
            ParseFailureMessages = parseFailureMessages ?? Array.Empty<string>();
        }

        public RoMapEntryDrainResult(
            RoMapEntryDrainedPacket[] packets,
            RoZcNotifyPlayerChat[] playerChats,
            RoZcNotifyTime[] timeNotifications,
            RoZcNotifyActorEntry[] actorEntries,
            RoZcNotifyMove[] actorMoves,
            RoZcNotifyPlayerMove[] playerMoves,
            RoZcStopMove[] stopMoves,
            RoZcNotifyVanish[] actorVanishes,
            bool stoppedOnUnsupportedPacket,
            string stopReason,
            int parseFailureCount = 0,
            string[] parseFailureMessages = null) : this(
            packets,
            playerChats,
            timeNotifications,
            actorEntries,
            actorMoves,
            playerMoves,
            stopMoves,
            actorVanishes,
            Array.Empty<RoMapEntryDrainEvent>(),
            stoppedOnUnsupportedPacket,
            stopReason,
            parseFailureCount,
            parseFailureMessages) {
        }

        public RoMapEntryDrainResult(
            RoMapEntryDrainedPacket[] packets,
            RoZcNotifyPlayerChat[] playerChats,
            RoZcNotifyTime[] timeNotifications,
            RoZcNotifyActorEntry[] actorEntries,
            RoZcNotifyMove[] actorMoves,
            RoZcNotifyPlayerMove[] playerMoves,
            RoZcStopMove[] stopMoves,
            RoZcNotifyVanish[] actorVanishes,
            RoMapEntryDrainEvent[] orderedEvents,
            bool stoppedOnUnsupportedPacket,
            string stopReason,
            int parseFailureCount = 0,
            string[] parseFailureMessages = null) {
            Packets = packets ?? Array.Empty<RoMapEntryDrainedPacket>();
            PlayerChats = playerChats ?? Array.Empty<RoZcNotifyPlayerChat>();
            TimeNotifications = timeNotifications ?? Array.Empty<RoZcNotifyTime>();
            ActorEntries = actorEntries ?? Array.Empty<RoZcNotifyActorEntry>();
            ActorMoves = actorMoves ?? Array.Empty<RoZcNotifyMove>();
            PlayerMoves = playerMoves ?? Array.Empty<RoZcNotifyPlayerMove>();
            StopMoves = stopMoves ?? Array.Empty<RoZcStopMove>();
            ActorVanishes = actorVanishes ?? Array.Empty<RoZcNotifyVanish>();
            OrderedEvents = orderedEvents ?? Array.Empty<RoMapEntryDrainEvent>();
            StoppedOnUnsupportedPacket = stoppedOnUnsupportedPacket;
            StopReason = stopReason ?? string.Empty;
            ParseFailureCount = Math.Max(0, parseFailureCount);
            ParseFailureMessages = parseFailureMessages ?? Array.Empty<string>();
        }

        public RoMapEntryDrainedPacket[] Packets { get; }
        public RoZcNotifyPlayerChat[] PlayerChats { get; }
        public RoZcNotifyTime[] TimeNotifications { get; }
        public RoZcNotifyActorEntry[] ActorEntries { get; }
        public RoZcNotifyMove[] ActorMoves { get; }
        public RoZcNotifyPlayerMove[] PlayerMoves { get; }
        public RoZcStopMove[] StopMoves { get; }
        public RoZcNotifyVanish[] ActorVanishes { get; }
        public RoMapEntryDrainEvent[] OrderedEvents { get; }
        public bool StoppedOnUnsupportedPacket { get; }
        public string StopReason { get; }
        public int ParseFailureCount { get; }
        public string[] ParseFailureMessages { get; }
    }

    public sealed class RoTcpPacketConnection : IDisposable {
        private readonly RoPacketRegistry registry;
        private readonly RoServerProfile profile;
        private readonly IRoPacketTraceSink traceSink;
        private readonly IRoPacketCaptureSink captureSink;
        private readonly IRoPacketTransform transform;
        private readonly bool enableVerboseTraceMessages;
        private readonly TcpClient client;
        private readonly NetworkStream stream;
        private readonly RoPacketStreamFramer inboundFramer;
        private readonly byte[] receiveBuffer;
        private int rodexOpenDiagnosticsRemaining;
        private bool disposed;

        private RoTcpPacketConnection(
            RoPacketRegistry registry,
            RoServerProfile profile,
            IRoPacketTraceSink traceSink,
            IRoPacketCaptureSink captureSink,
            IRoPacketTransform transform,
            TcpClient client,
            bool enableVerboseTraceMessages) {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.profile = profile;
            this.traceSink = traceSink;
            this.captureSink = captureSink;
            this.transform = transform ?? RoNoPacketTransform.Instance;
            this.enableVerboseTraceMessages = enableVerboseTraceMessages;
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            stream = client.GetStream();
            inboundFramer = new RoPacketStreamFramer(registry, RoPacketDirection.ServerToClient);
            receiveBuffer = new byte[8192];
        }

        public static RoTcpPacketConnection Connect(
            RoServerEndpoint endpoint,
            RoPacketRegistry registry,
            RoServerProfile profile,
            IRoPacketTraceSink traceSink = null,
            IRoPacketTransform transform = null,
            int timeoutMilliseconds = 5000,
            IRoPacketCaptureSink captureSink = null,
            bool enableVerboseTraceMessages = false) {
            if (string.IsNullOrWhiteSpace(endpoint.Host)) {
                throw new ArgumentException("Endpoint host is required.", nameof(endpoint));
            }

            if (endpoint.Port <= 0 || endpoint.Port > ushort.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint.Port, "Endpoint port must be a TCP port.");
            }

            var client = new TcpClient {
                ReceiveTimeout = timeoutMilliseconds,
                SendTimeout = timeoutMilliseconds,
                NoDelay = true,
            };

            client.Connect(endpoint.Host, endpoint.Port);
            return new RoTcpPacketConnection(registry, profile, traceSink, captureSink, transform, client, enableVerboseTraceMessages);
        }

        public int AvailableBytes {
            get {
                ThrowIfDisposed();
                return client.Available;
            }
        }

        public bool WaitForAvailableData(int timeoutMilliseconds) {
            ThrowIfDisposed();
            if (HasBufferedFrameOrTerminalStatus()) {
                return true;
            }

            if (timeoutMilliseconds <= 0) {
                if (client.Available > 0) {
                    ReadAvailableIntoFramer();
                    return HasBufferedFrameOrTerminalStatus();
                }

                return false;
            }

            int start = Environment.TickCount;
            while (Environment.TickCount - start < timeoutMilliseconds) {
                if (client.Available > 0) {
                    ReadAvailableIntoFramer();
                    if (HasBufferedFrameOrTerminalStatus()) {
                        return true;
                    }
                }

                Thread.Sleep(1);
            }

            if (client.Available > 0) {
                ReadAvailableIntoFramer();
            }

            return HasBufferedFrameOrTerminalStatus();
        }

        public void Send(byte[] packet) {
            ThrowIfDisposed();
            if (packet == null) {
                throw new ArgumentNullException(nameof(packet));
            }

            if (packet.Length < 2) {
                throw new ArgumentException("Packet must include a 2-byte packet id.", nameof(packet));
            }

            var bytes = new byte[packet.Length];
            Array.Copy(packet, bytes, packet.Length);
            transform.TransformOutbound(bytes, 0, bytes.Length, profile);
            stream.Write(bytes, 0, bytes.Length);

            ushort packetId = (ushort) (bytes[0] | (bytes[1] << 8));
            registry.TryGet(packetId, out RoPacketDefinition definition);
            Trace(
                RoPacketDirection.ClientToServer,
                packetId,
                definition.Name,
                bytes.Length,
                RoPacketFrameStatus.Complete,
                ShouldBuildVerboseTraceMessage() ? FormatTraceMessage(packetId, bytes) : null);
            Capture(RoPacketDirection.ClientToServer, packetId, definition.Name, bytes, RoPacketFrameStatus.Complete);
            RoNetworkRuntimeDiagnostics.RecordOutbound(packetId, bytes.Length, definition.Name);
            ObserveRodexOutbound(packetId, bytes.Length);
        }

        public byte[] ReadRaw(int byteCount, string traceName) {
            ThrowIfDisposed();
            byte[] bytes = ReadExact(byteCount);
            Trace(RoPacketDirection.ServerToClient, 0, traceName, bytes.Length, RoPacketFrameStatus.Complete);
            Capture(RoPacketDirection.ServerToClient, 0, traceName, bytes, RoPacketFrameStatus.Complete);
            return bytes;
        }

        public RoPacketFrame ReadFrame() {
            ThrowIfDisposed();
            while (true) {
                if (TryReadBufferedFrame(out RoPacketFrame frame)) {
                    return frame;
                }

                int read = stream.Read(receiveBuffer, 0, receiveBuffer.Length);
                if (read <= 0) {
                    throw new EndOfStreamException("Socket closed while reading packet bytes.");
                }

                inboundFramer.Append(receiveBuffer, 0, read);
            }
        }

        public void Dispose() {
            if (disposed) {
                return;
            }

            disposed = true;
            stream.Dispose();
            client.Dispose();
        }

        private byte[] ReadExact(int byteCount) {
            if (byteCount < 0) {
                throw new ArgumentOutOfRangeException(nameof(byteCount));
            }

            var bytes = new byte[byteCount];
            int offset = 0;
            while (offset < byteCount) {
                int read = stream.Read(bytes, offset, byteCount - offset);
                if (read <= 0) {
                    throw new EndOfStreamException("Socket closed while reading packet bytes.");
                }

                offset += read;
            }

            return bytes;
        }

        private bool TryReadBufferedFrame(out RoPacketFrame frame) {
            if (inboundFramer.TryReadFrame(out frame, out RoPacketFrameMeasurement measurement)) {
                byte[] bytes = frame.Bytes;
                transform.TransformInbound(bytes, 0, bytes.Length, profile);
                Trace(
                    RoPacketDirection.ServerToClient,
                    frame.PacketId,
                    frame.Definition.Name,
                    frame.Length,
                    RoPacketFrameStatus.Complete,
                    ShouldBuildVerboseTraceMessage() ? FormatTraceMessage(frame.PacketId, bytes) : null);
                Capture(RoPacketDirection.ServerToClient, frame.PacketId, frame.Definition.Name, bytes, RoPacketFrameStatus.Complete);
                RoNetworkRuntimeDiagnostics.RecordInbound(frame.PacketId, frame.Length, frame.Definition.Name);
                ObserveRodexInbound(frame);
                return true;
            }

            switch (measurement.Status) {
                case RoPacketFrameStatus.NeedMoreData:
                    return false;
                case RoPacketFrameStatus.UnknownPacket: {
                    byte[] header = inboundFramer.CopyBufferedPrefix(Math.Min(16, inboundFramer.BufferedByteCount));
                    Trace(RoPacketDirection.ServerToClient, measurement.PacketId, string.Empty, header.Length, RoPacketFrameStatus.UnknownPacket);
                    Capture(RoPacketDirection.ServerToClient, measurement.PacketId, string.Empty, header, RoPacketFrameStatus.UnknownPacket);
                    RecordFrameDiagnostic(measurement, header, "Legacy map inbound frame error");
                    ObserveRodexFrameError(measurement, header);
                    throw new RoNetworkProtocolException(
                        $"Unknown packet 0x{measurement.PacketId:x4}. No known 20200401 length fallback exists; buffered {inboundFramer.BufferedByteCount} byte(s); prefix={RoNetworkRuntimeDiagnostics.FormatPacketPrefix(header, 24)}.");
                }
                case RoPacketFrameStatus.InvalidLength: {
                    byte[] invalidFrame = inboundFramer.CopyBufferedPrefix(Math.Min(4, inboundFramer.BufferedByteCount));
                    string packetName = measurement.Definition.Name ?? string.Empty;
                    Trace(RoPacketDirection.ServerToClient, measurement.PacketId, packetName, measurement.FrameLength, RoPacketFrameStatus.InvalidLength);
                    Capture(RoPacketDirection.ServerToClient, measurement.PacketId, packetName, invalidFrame, RoPacketFrameStatus.InvalidLength);
                    RecordFrameDiagnostic(measurement, invalidFrame, "Legacy map inbound frame error");
                    ObserveRodexFrameError(measurement, invalidFrame);
                    throw new RoNetworkProtocolException($"Packet 0x{measurement.PacketId:x4} has invalid variable length {measurement.FrameLength}; prefix={RoNetworkRuntimeDiagnostics.FormatPacketPrefix(invalidFrame, 24)}.");
                }
                default:
                    throw new RoNetworkProtocolException($"Unsupported packet frame status {measurement.Status} for packet 0x{measurement.PacketId:x4}.");
            }
        }

        private bool HasBufferedFrameOrTerminalStatus() {
            RoPacketFrameMeasurement measurement = inboundFramer.Peek();
            return measurement.Status != RoPacketFrameStatus.NeedMoreData;
        }

        internal void ThrowIfRemoteSocketClosed() {
            Socket socket = client.Client;
            if (socket != null &&
                socket.Poll(0, SelectMode.SelectRead) &&
                client.Available == 0) {
                throw new EndOfStreamException("Socket closed while reading packet bytes.");
            }
        }

        private void ObserveRodexOutbound(ushort packetId, int length) {
            if (packetId != RoPacketIds.CzOpenAllRodex) {
                return;
            }

            rodexOpenDiagnosticsRemaining = 8;
        }

        private void ObserveRodexInbound(RoPacketFrame frame) {
            if (rodexOpenDiagnosticsRemaining <= 0) {
                return;
            }

            rodexOpenDiagnosticsRemaining--;
            if (frame.PacketId == RoPacketIds.ZcAckRodexList3 ||
                frame.PacketId == RoPacketIds.ZcAckRodexList4 ||
                frame.PacketId == RoPacketIds.ZcAckFailedAllRodexList ||
                frame.PacketId == RoPacketIds.ZcNpcChat ||
                rodexOpenDiagnosticsRemaining <= 0) {
                rodexOpenDiagnosticsRemaining = 0;
            }
        }

        private void ObserveRodexFrameError(RoPacketFrameMeasurement measurement, byte[] prefix) {
            if (rodexOpenDiagnosticsRemaining <= 0) {
                return;
            }

            string prefixText = RoNetworkRuntimeDiagnostics.FormatPacketPrefix(prefix, 24);
            RoNetworkRuntimeDiagnostics.RecordDiagnostic(
                $"RODEX inbound after open failed opcode=0x{measurement.PacketId:x4} len={measurement.FrameLength} status={measurement.Status} prefix={prefixText}",
                measurement.PacketId,
                measurement.Definition.Name,
                measurement.Status,
                measurement.FrameLength,
                prefixText);
            rodexOpenDiagnosticsRemaining = 0;
        }

        private void RecordFrameDiagnostic(RoPacketFrameMeasurement measurement, byte[] prefix, string reason) {
            string prefixText = RoNetworkRuntimeDiagnostics.FormatPacketPrefix(prefix, 24);
            string packetName = measurement.Definition.Name ?? string.Empty;
            string expected = measurement.Definition.Length > 0
                ? measurement.Definition.Length.ToString()
                : (measurement.Definition.IsVariableLength ? "variable" : "?");
            RoNetworkRuntimeDiagnostics.RecordDiagnostic(
                $"{reason}: opcode=0x{measurement.PacketId:x4} name={(string.IsNullOrWhiteSpace(packetName) ? "?" : packetName)} status={measurement.Status} expected={expected} actual={measurement.FrameLength} buffered={inboundFramer.BufferedByteCount} prefix={prefixText}",
                measurement.PacketId,
                packetName,
                measurement.Status,
                measurement.FrameLength,
                prefixText);
        }

        private void ReadAvailableIntoFramer() {
            do {
                int bytesToRead = Math.Min(receiveBuffer.Length, Math.Max(1, client.Available));
                int read = stream.Read(receiveBuffer, 0, bytesToRead);
                if (read <= 0) {
                    throw new EndOfStreamException("Socket closed while reading packet bytes.");
                }

                inboundFramer.Append(receiveBuffer, 0, read);
            } while (client.Available > 0);
        }

        private void Trace(RoPacketDirection direction, ushort packetId, string packetName, int length, RoPacketFrameStatus status, string message = null) {
            if (traceSink == null) {
                return;
            }

            if (!traceSink.IsEnabled) {
                return;
            }

            traceSink?.Write(new RoPacketTraceEntry(
                DateTimeOffset.UtcNow,
                direction,
                packetId,
                packetName ?? string.Empty,
                length,
                status,
                message));
        }

        private bool ShouldBuildVerboseTraceMessage() {
            return enableVerboseTraceMessages && traceSink != null && traceSink.IsEnabled;
        }

        private static string FormatTraceMessage(ushort packetId, byte[] frame) {
            if (frame == null) {
                return string.Empty;
            }

            switch (packetId) {
                case RoPacketIds.CzRequestChat:
                    if (frame.Length >= 4) {
                        return $"text=\"{FormatTraceText(frame, 4)}\"";
                    }

                    break;
                case RoPacketIds.ZcNotifyChat:
                    if (frame.Length >= 8) {
                        return $"text=\"{FormatTraceText(frame, 8)}\"";
                    }

                    break;
                case RoPacketIds.ZcNotifyPlayerChat:
                    if (frame.Length >= 4) {
                        return $"text=\"{FormatTraceText(frame, 4)}\"";
                    }

                    break;
                case RoPacketIds.CzItemThrow2:
                    if (frame.Length >= 6) {
                        ushort bagIndex = ReadUInt16(frame, 2);
                        ushort count = ReadUInt16(frame, 4);
                        return $"bag={bagIndex} count={count}";
                    }

                    break;
                case RoPacketIds.CzReqWearEquipV5:
                    if (frame.Length >= 8) {
                        ushort bagIndex = ReadUInt16(frame, 2);
                        uint position = ReadUInt32(frame, 4);
                        return $"bag={bagIndex} pos=0x{position:x8}";
                    }

                    break;
                case RoPacketIds.CzReqTakeoffEquip:
                    if (frame.Length >= 4) {
                        ushort bagIndex = ReadUInt16(frame, 2);
                        return $"bag={bagIndex}";
                    }

                    break;
                case RoPacketIds.ZcAckWearEquipV5:
                    if (frame.Length >= 11) {
                        ushort bagIndex = ReadUInt16(frame, 2);
                        uint position = ReadUInt32(frame, 4);
                        ushort viewId = ReadUInt16(frame, 8);
                        byte result = frame[10];
                        return $"bag={bagIndex} pos=0x{position:x8} view={viewId} result={result}";
                    }

                    break;
                case RoPacketIds.ZcAckTakeoffEquipV5:
                    if (frame.Length >= 9) {
                        ushort bagIndex = ReadUInt16(frame, 2);
                        uint position = ReadUInt32(frame, 4);
                        byte result = frame[8];
                        return $"bag={bagIndex} pos=0x{position:x8} result={result}";
                    }

                    break;
                case RoPacketIds.ZcReqTakeoffEquipAck:
                case RoPacketIds.ZcReqTakeoffEquipAck2:
                    if (frame.Length >= 7) {
                        ushort bagIndex = ReadUInt16(frame, 2);
                        ushort position = ReadUInt16(frame, 4);
                        byte result = frame[6];
                        return $"bag={bagIndex} pos=0x{position:x4} result={result}";
                    }

                    break;
                case RoPacketIds.ZcEquipArrow:
                    if (frame.Length >= 4) {
                        ushort bagIndex = ReadUInt16(frame, 2);
                        return $"bag={bagIndex}";
                    }

                    break;
                case RoPacketIds.ZcItemThrowAck:
                    if (frame.Length >= 6) {
                        ushort bagIndex = ReadUInt16(frame, 2);
                        ushort count = ReadUInt16(frame, 4);
                        return $"bag={bagIndex} count={count}";
                    }

                    break;
                case RoPacketIds.ZcActionFailure:
                    if (frame.Length >= 4) {
                        ushort type = ReadUInt16(frame, 2);
                        return $"type={type}";
                    }

                    break;
                case RoPacketIds.ZcItemPickupAck7:
                    if (frame.Length >= 35) {
                        ushort bagIndex = ReadUInt16(frame, 2);
                        ushort count = ReadUInt16(frame, 4);
                        uint itemId = ReadUInt32(frame, 6);
                        byte result = frame[34];
                        return $"bag={bagIndex} item={itemId} count={count} result={result}";
                    }

                    break;
                case RoPacketIds.ZcItemPickupAck8:
                    if (frame.Length >= 35) {
                        ushort bagIndex = ReadUInt16(frame, 2);
                        short count = unchecked((short) ReadUInt16(frame, 4));
                        uint itemId = ReadUInt32(frame, 6);
                        byte result = frame[33];
                        return $"bag={bagIndex} item={itemId} count={count} result={result}";
                    }

                    break;
            }

            return string.Empty;
        }

        private static string FormatTraceText(byte[] frame, int offset) {
            string message = Encoding.ASCII.GetString(frame, offset, frame.Length - offset).TrimEnd('\0');
            if (message.Length > 80) {
                return message.Substring(0, 80);
            }

            return message;
        }

        private static ushort ReadUInt16(byte[] bytes, int offset) {
            return unchecked((ushort) (bytes[offset] | (bytes[offset + 1] << 8)));
        }

        private static uint ReadUInt32(byte[] bytes, int offset) {
            return unchecked((uint) (
                bytes[offset] |
                (bytes[offset + 1] << 8) |
                (bytes[offset + 2] << 16) |
                (bytes[offset + 3] << 24)));
        }

        private void Capture(RoPacketDirection direction, ushort packetId, string packetName, byte[] bytes, RoPacketFrameStatus status) {
            captureSink?.Write(
                DateTimeOffset.UtcNow,
                direction,
                packetId,
                packetName ?? string.Empty,
                bytes,
                status);
        }

        private void ThrowIfDisposed() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(RoTcpPacketConnection));
            }
        }
    }

    public sealed class RoLiveMapSession : IDisposable {
        private readonly object stateSync = new object();
        private readonly object sendSync = new object();
        private readonly object receiveSync = new object();
        private readonly RoLoginSessionClient owner;
        private readonly RoTcpPacketConnection connection;
        private bool disposed;

        internal RoLiveMapSession(RoLoginSessionClient owner, RoTcpPacketConnection connection, int localAid) {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
            LocalAid = localAid;
        }

        public int LocalAid { get; }

        public bool IsDisposed {
            get {
                lock (stateSync) {
                    return disposed;
                }
            }
        }

        public int AvailableBytes {
            get {
                lock (receiveSync) {
                    ThrowIfDisposed();
                    return connection.AvailableBytes;
                }
            }
        }

        public void Send(byte[] packet) {
            lock (sendSync) {
                ThrowIfDisposed();
                connection.Send(packet);
                // mode msg 0x45 (0xc1b5ca): a start with a one-shot use pending (a mouse use) ends at once, in its own send
                if (RoHeldSkillUse.IsStart(packet) && RoHeldSkillUse.OnStartSent()) connection.Send(RoHeldSkillUse.BuildEnd());
            }
        }

        public byte[] BuildMoveRequest(int x, int y, int direction) {
            return owner.BuildCzRequestMove(x, y, direction);
        }

        public byte[] BuildAttackRequest(uint targetAid, bool repeatAttack) {
            byte actionType = repeatAttack ? (byte) 7 : (byte) 0;
            return owner.BuildCzActionRequest(targetAid, actionType);
        }

        public byte[] BuildPickupGroundItemRequest(uint itemAid) {
            return owner.BuildCzItemPickup(itemAid);
        }

        public byte[] BuildTargetSkillRequest(int skillLevel, int skillId, uint targetAid) {
            return owner.BuildCzUseSkill(skillLevel, skillId, targetAid);
        }

        public byte[] BuildActorNameRequest(uint actorId) {
            return owner.BuildCzRequestName(actorId);
        }

        public byte[] BuildSitStandRequest(bool sit) {
            return owner.BuildCzActionRequest(
                0,
                sit ? (byte) RoMapActorActionType.SitDown : (byte) RoMapActorActionType.StandUp);
        }

        public byte[] BuildRestartRequest(byte type) {
            return owner.BuildCzRestart(type);
        }

        public byte[] BuildStandingResurrectionRequest() {
            return owner.BuildCzStandingResurrection();
        }

        public RoMapEntryDrainResult Drain(int maxFrames = 16, int idleMilliseconds = 100, bool stopAfterMovementResponse = false) {
            lock (receiveSync) {
                ThrowIfDisposed();
                RoMapEntryDrainResult drain = owner.DrainLiveMapSession(connection, maxFrames, idleMilliseconds, stopAfterMovementResponse);
                // an idle drain never reads (it waits on Available), so a socket the server closed (a kick, a second
                // login on the account, a server stop) would only surface at the next keep-alive send: check it here
                if (drain == null || drain.Packets.Length == 0) connection.ThrowIfRemoteSocketClosed();
                return drain;
            }
        }

        public RoMapEntryDrainResult SendAndDrain(byte[] packet, int maxFrames = 16, int idleMilliseconds = 100) {
            lock (receiveSync) {
                ThrowIfDisposed();
                lock (sendSync) {
                    ThrowIfDisposed();
                    connection.Send(packet);
                }

                return owner.DrainLiveMapSession(connection, maxFrames, idleMilliseconds);
            }
        }

        public RoLoginEnterMapResult AcknowledgeSameServerMapMove(
            RoLoginEnterMapResult previousResult,
            RoZcNpcMapMove mapMove,
            RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck = null,
            int maxFrames = 96,
            int idleMilliseconds = 1000,
            bool requireInitialState = true) {
            lock (receiveSync) {
                ThrowIfDisposed();
                owner.WaitForRuntimeMapLoadBeforeAck(loadRuntimeMapBeforeAck, mapMove.MapName, mapMove.X, mapMove.Y);
                lock (sendSync) {
                    ThrowIfDisposed();
                    connection.Send(owner.BuildCzNotifyActorInit());
                }

                int drainIdleMilliseconds = requireInitialState ? idleMilliseconds : 25;
                RoMapEntryDrainResult initialState = owner.DrainLiveMapSession(connection, maxFrames, drainIdleMilliseconds) ??
                    RoMapEntryDrainResult.Empty;
                if (!requireInitialState && initialState.Packets.Length == 0) {
                    connection.ThrowIfRemoteSocketClosed();
                }

                if (requireInitialState && initialState.Packets.Length == 0) {
                    throw new RoNetworkProtocolException(
                        $"Same-server map move to {mapMove.MapName} {mapMove.X},{mapMove.Y} did not receive initial map state within {idleMilliseconds} ms after CZ_NOTIFY_ACTORINIT.");
                }

                if (initialState.StoppedOnUnsupportedPacket) {
                    string stopReason = string.IsNullOrWhiteSpace(initialState.StopReason)
                        ? "Same-server map move initial state stopped on unsupported packet."
                        : initialState.StopReason;
                    throw new RoNetworkProtocolException(stopReason);
                }

                return owner.CreateSameServerMapMoveResult(previousResult, mapMove, initialState, this);
            }
        }

        public RoLoginEnterMapResult ReconnectMapServer(
            RoLoginEnterMapResult previousResult,
            RoZcNpcServerMove serverMove,
            RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck = null) {
            ThrowIfDisposed();
            return owner.EnterRedirectedMap(previousResult, serverMove, loadRuntimeMapBeforeAck);
        }

        public void Dispose() {
            lock (stateSync) {
                if (disposed) {
                    return;
                }

                disposed = true;
                connection.Dispose();
            }
        }

        private void ThrowIfDisposed() {
            lock (stateSync) {
                if (disposed) {
                    throw new ObjectDisposedException(nameof(RoLiveMapSession));
                }
            }
        }
    }

    public sealed class RoLoginSessionClient {
        private const string ProcessBatchMarkerName = "RO.Network.ProcessBatch";

        private readonly RoServerProfile profile;
        private readonly RoPacketRegistry registry;
        private readonly IRoPacketTransform transform;
        private readonly IRoPacketCaptureSink captureSink;
        private readonly RoProfiledOutboundPackets outboundPackets;
        private readonly Encoding textEncoding;
        private readonly bool enableVerboseTraceMessages;

        private readonly struct NetworkProfilerScope : IDisposable {
            public void Dispose() {
            }
        }

        private static NetworkProfilerScope BeginProcessBatchSample() {
            // Ragnarok.Network is a pure protocol assembly without UnityEngine/Unity.Profiling references.
            // Keep the marker name here for traceability; Unity-facing callers wrap this batch at their boundary.
            _ = ProcessBatchMarkerName;
            return default;
        }

        public RoLoginSessionClient(
            RoServerProfile profile,
            RoPacketRegistry registry = null,
            IRoPacketTransform transform = null,
            IRoPacketTraceSink traceSink = null,
            IRoPacketCaptureSink captureSink = null,
            int timeoutMilliseconds = 5000,
            bool enableVerboseTraceMessages = false) {
            this.profile = profile;
            IRoPacketProfile packetProfile = RoPacketProfiles.ForServerProfile(profile);
            this.registry = registry ?? packetProfile.CreateRegistry();
            this.transform = transform ?? packetProfile.CreatePacketTransform();
            this.captureSink = captureSink;
            this.enableVerboseTraceMessages = enableVerboseTraceMessages;
            outboundPackets = new RoProfiledOutboundPackets(packetProfile, this.registry);
            TimeoutMilliseconds = timeoutMilliseconds;

            TraceBuffer = traceSink as RoPacketTraceBuffer ?? new RoPacketTraceBuffer { IsEnabled = false };
            TraceSink = traceSink;
            textEncoding = ResolveEncoding(profile.LegacyTextCodePage);
        }

        public int TimeoutMilliseconds { get; }
        public RoPacketTraceBuffer TraceBuffer { get; }
        public IRoPacketTraceSink TraceSink { get; }
        public RoLoginSessionState State { get; private set; }

        public Task<RoLoginSessionResult> LoginAndLoadCharactersAsync(string account, string password, CancellationToken cancellationToken = default) {
            return Task.Run(() => LoginAndLoadCharacters(account, password), cancellationToken);
        }

        public Task<RoLoginCreateCharacterResult> LoginCreateCharacterAndSelectAsync(
            string account,
            string password,
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null,
            CancellationToken cancellationToken = default) {
            return Task.Run(
                () => LoginCreateCharacterAndSelect(account, password, characterName, characterSlot, hairColor, hairStyle, startJob, sex),
                cancellationToken);
        }

        public Task<RoLoginCreateCharacterResult> LoginCreateCharacterAsync(
            string account,
            string password,
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null,
            CancellationToken cancellationToken = default) {
            return Task.Run(
                () => LoginCreateCharacter(account, password, characterName, characterSlot, hairColor, hairStyle, startJob, sex),
                cancellationToken);
        }

        public Task<RoLoginEnterMapResult> LoginCreateCharacterSelectAndEnterMapAsync(
            string account,
            string password,
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null,
            CancellationToken cancellationToken = default) {
            return Task.Run(
                () => LoginCreateCharacterSelectAndEnterMap(account, password, characterName, characterSlot, hairColor, hairStyle, startJob, sex),
                cancellationToken);
        }

        public Task<RoLoginEnterMapResult> LoginCreateCharacterSelectEnterMapAndLoadInitialStateAsync(
            string account,
            string password,
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null,
            CancellationToken cancellationToken = default) {
            return Task.Run(
                () => LoginCreateCharacterSelectEnterMapAndLoadInitialState(account, password, characterName, characterSlot, hairColor, hairStyle, startJob, sex),
                cancellationToken);
        }

        public Task<RoLoginEnterMapResult> LoginCreateCharacterSelectEnterMapLoadInitialStateAndRequestMoveAsync(
            string account,
            string password,
            string characterName,
            int moveX,
            int moveY,
            int moveDirection = 0,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null,
            CancellationToken cancellationToken = default) {
            return Task.Run(
                () => LoginCreateCharacterSelectEnterMapLoadInitialStateAndRequestMove(
                    account,
                    password,
                    characterName,
                    moveX,
                    moveY,
                    moveDirection,
                    characterSlot,
                    hairColor,
                    hairStyle,
                    startJob,
                    sex),
                cancellationToken);
        }

        public Task<RoLoginEnterMapResult> LoginCreateCharacterSelectEnterMapLoadInitialStateAndOpenSessionAsync(
            string account,
            string password,
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null,
            RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck = null,
            CancellationToken cancellationToken = default) {
            return Task.Run(
                () => LoginCreateCharacterSelectEnterMapLoadInitialStateAndOpenSession(
                    account,
                    password,
                    characterName,
                    characterSlot,
                    hairColor,
                    hairStyle,
                    startJob,
                    sex,
                    loadRuntimeMapBeforeAck),
                cancellationToken);
        }

        public RoLoginSessionResult LoginAndLoadCharacters(string account, string password) {
            if (string.IsNullOrWhiteSpace(account)) {
                throw new ArgumentException("Account is required.", nameof(account));
            }

            try {
                RoAcAcceptLogin3 login = Login(account, password ?? string.Empty);
                if (login.Servers.Length == 0) {
                    throw new RoNetworkProtocolException("Login succeeded, but no character servers were returned.");
                }

                RoLoginCharServerInfo selectedServer = login.Servers[0];
                RoInitialCharacterSnapshot characters = LoadCharacters(login, selectedServer);
                State = RoLoginSessionState.CharacterListReceived;
                return new RoLoginSessionResult(profile, login, selectedServer, characters);
            } catch {
                State = RoLoginSessionState.Failed;
                throw;
            }
        }

        public RoLoginCreateCharacterResult LoginCreateCharacterAndSelect(
            string account,
            string password,
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null) {
            if (string.IsNullOrWhiteSpace(account)) {
                throw new ArgumentException("Account is required.", nameof(account));
            }

            if (string.IsNullOrWhiteSpace(characterName)) {
                throw new ArgumentException("Character name is required.", nameof(characterName));
            }

            try {
                RoAcAcceptLogin3 login = Login(account, password ?? string.Empty);
                if (login.Servers.Length == 0) {
                    throw new RoNetworkProtocolException("Login succeeded, but no character servers were returned.");
                }

                RoLoginCharServerInfo selectedServer = login.Servers[0];
                RoServerEndpoint endpoint = ResolveCharacterEndpoint(selectedServer);
                using (RoTcpPacketConnection connection = RoTcpPacketConnection.Connect(
                    endpoint,
                    registry,
                    profile,
                    TraceSink,
                    transform,
                    TimeoutMilliseconds,
                    captureSink,
                    enableVerboseTraceMessages)) {
                    RoInitialCharacterSnapshot initialCharacters = EnterCharacterServer(connection, login);
                    RoCharacterInfo selectedCharacter = FindCharacterByName(initialCharacters.Characters, characterName);
                    bool characterWasCreated = selectedCharacter == null;
                    RoHcAcceptMakeChar createdCharacter = default;
                    if (characterWasCreated) {
                        createdCharacter = CreateCharacter(
                            connection,
                            characterName,
                            characterSlot,
                            hairColor,
                            hairStyle,
                            startJob,
                            sex ?? login.Sex);
                        selectedCharacter = createdCharacter.Character;
                    }

                    RoHcNotifyZoneServer2 mapHandoff = SelectCharacter(connection, selectedCharacter.Slot);
                    State = RoLoginSessionState.MapHandoffReceived;
                    return characterWasCreated
                        ? new RoLoginCreateCharacterResult(profile, login, selectedServer, initialCharacters, createdCharacter, mapHandoff)
                        : new RoLoginCreateCharacterResult(profile, login, selectedServer, initialCharacters, selectedCharacter, false, mapHandoff);
                }
            } catch {
                State = RoLoginSessionState.Failed;
                throw;
            }
        }

        public RoLoginCreateCharacterResult LoginCreateCharacter(
            string account,
            string password,
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null) {
            if (string.IsNullOrWhiteSpace(account)) {
                throw new ArgumentException("Account is required.", nameof(account));
            }

            if (string.IsNullOrWhiteSpace(characterName)) {
                throw new ArgumentException("Character name is required.", nameof(characterName));
            }

            try {
                RoAcAcceptLogin3 login = Login(account, password ?? string.Empty);
                if (login.Servers.Length == 0) {
                    throw new RoNetworkProtocolException("Login succeeded, but no character servers were returned.");
                }

                RoLoginCharServerInfo selectedServer = login.Servers[0];
                RoServerEndpoint endpoint = ResolveCharacterEndpoint(selectedServer);
                using (RoTcpPacketConnection connection = RoTcpPacketConnection.Connect(
                    endpoint,
                    registry,
                    profile,
                    TraceSink,
                    transform,
                    TimeoutMilliseconds,
                    captureSink,
                    enableVerboseTraceMessages)) {
                    RoInitialCharacterSnapshot initialCharacters = EnterCharacterServer(connection, login);
                    RoCharacterInfo selectedCharacter = FindCharacterByName(initialCharacters.Characters, characterName);
                    if (selectedCharacter != null) {
                        State = RoLoginSessionState.CharacterListReceived;
                        return new RoLoginCreateCharacterResult(
                            profile,
                            login,
                            selectedServer,
                            initialCharacters,
                            selectedCharacter,
                            characterWasCreated: false,
                            default);
                    }

                    RoHcAcceptMakeChar createdCharacter = CreateCharacter(
                        connection,
                        characterName,
                        characterSlot,
                        hairColor,
                        hairStyle,
                        startJob,
                        sex ?? login.Sex);
                    return new RoLoginCreateCharacterResult(
                        profile,
                        login,
                        selectedServer,
                        initialCharacters,
                        createdCharacter,
                        default);
                }
            } catch {
                State = RoLoginSessionState.Failed;
                throw;
            }
        }

        public RoLoginEnterMapResult LoginCreateCharacterSelectAndEnterMap(
            string account,
            string password,
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null) {
            RoLoginCreateCharacterResult characterResult = LoginCreateCharacterAndSelect(
                account,
                password,
                characterName,
                characterSlot,
                hairColor,
                hairStyle,
                startJob,
                sex);
            return EnterMap(characterResult, acknowledgeLoadEnd: false);
        }

        public RoLoginEnterMapResult LoginCreateCharacterSelectEnterMapAndLoadInitialState(
            string account,
            string password,
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null) {
            RoLoginCreateCharacterResult characterResult = LoginCreateCharacterAndSelect(
                account,
                password,
                characterName,
                characterSlot,
                hairColor,
                hairStyle,
                startJob,
                sex);
            return EnterMap(characterResult, acknowledgeLoadEnd: true);
        }

        public RoLoginEnterMapResult LoginCreateCharacterSelectEnterMapLoadInitialStateAndRequestMove(
            string account,
            string password,
            string characterName,
            int moveX,
            int moveY,
            int moveDirection = 0,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null) {
            RoLoginCreateCharacterResult characterResult = LoginCreateCharacterAndSelect(
                account,
                password,
                characterName,
                characterSlot,
                hairColor,
                hairStyle,
                startJob,
                sex);
            return EnterMap(characterResult, acknowledgeLoadEnd: true, moveRequest: new RoMapMoveRequest(moveX, moveY, moveDirection));
        }

        public RoLoginEnterMapResult LoginCreateCharacterSelectEnterMapLoadInitialStateAndOpenSession(
            string account,
            string password,
            string characterName,
            int characterSlot = 0,
            ushort hairColor = 0,
            ushort hairStyle = 1,
            int startJob = 0,
            byte? sex = null,
            RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck = null) {
            RoLoginCreateCharacterResult characterResult = LoginCreateCharacterAndSelect(
                account,
                password,
                characterName,
                characterSlot,
                hairColor,
                hairStyle,
                startJob,
                sex);
            return EnterMap(
                characterResult,
                acknowledgeLoadEnd: true,
                moveRequest: null,
                keepMapConnectionAlive: true,
                loadRuntimeMapBeforeAck: loadRuntimeMapBeforeAck);
        }

        public RoLoginEnterMapResult EnterMapFromHandoff(
            RoCharSelectFlowResult selection,
            RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck = null) {
            if (selection == null) {
                throw new ArgumentNullException(nameof(selection));
            }

            RoCharFlowResult characterFlow = selection.CharacterFlow;
            RoLoginFlowResult loginFlow = characterFlow.Login;
            RoAcAcceptLogin3 login = loginFlow.Login;
            return EnterMapFromHandoff(
                login.AccountId,
                login.LoginId1,
                login.LoginId2,
                login.Sex,
                selection.SelectedCharacter.CharacterId,
                selection.SelectedCharacter,
                selection.MapHandoff,
                characterFlow.Characters,
                loginFlow.SelectedCharacterServer,
                loadRuntimeMapBeforeAck);
        }

        public RoLoginEnterMapResult EnterMapFromHandoff(
            int accountId,
            int loginId1,
            int loginId2,
            byte sex,
            int characterId,
            RoCharacterInfo selectedCharacter,
            RoHcNotifyZoneServer2 mapHandoff,
            RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck = null) {
            return EnterMapFromHandoff(
                accountId,
                loginId1,
                loginId2,
                sex,
                characterId,
                selectedCharacter,
                mapHandoff,
                null,
                default,
                loadRuntimeMapBeforeAck);
        }

        private RoAcAcceptLogin3 Login(string account, string password) {
            using (RoTcpPacketConnection connection = RoTcpPacketConnection.Connect(
                profile.Login,
                registry,
                profile,
                TraceSink,
                transform,
                TimeoutMilliseconds,
                captureSink,
                enableVerboseTraceMessages)) {
                State = RoLoginSessionState.LoginConnected;
                if (RoServerSignature.Required) {
                    // RoServerSignature: a server without it closes the connection or answers something else - refused
                    byte[] challenge = RoServerSignature.NewChallenge();
                    connection.Send(RoServerSignature.BuildRequest(challenge));
                    RoPacketFrame reply;
                    try {
                        reply = connection.ReadFrame();
                    } catch (Exception exception) when (exception is IOException || exception is SocketException || exception is RoNetworkProtocolException) {
                        throw new RoNetworkProtocolException(RoServerSignature.RefusedMessage);
                    }

                    RoServerSignature.Check(challenge, reply.Bytes);
                }

                connection.Send(outboundPackets.BuildCaLogin(account, RoLauncherTicket.ResolvePassword(account, password)));
                RoPacketFrame frame = connection.ReadFrame();

                if (frame.PacketId == RoPacketIds.AcRefuseLoginR2 ||
                    frame.PacketId == RoPacketIds.AcRefuseLogin20240619) {
                    RoAcRefuseLogin refuse = RoLoginPacketParser.ParseAcRefuseLogin(frame.Bytes, textEncoding);
                    throw new RoNetworkProtocolException($"Login refused with error code {refuse.ErrorCode}.");
                }

                if (frame.PacketId == RoPacketIds.ScNotifyBan) {
                    RoScNotifyBan ban = RoMapPacketParser.ParseScNotifyBan(frame.Bytes);
                    throw new RoNetworkProtocolException($"Login rejected by server with auth result code {ban.ErrorCode}.");
                }

                if (frame.PacketId != RoPacketIds.AcAcceptLogin3) {
                    throw new RoNetworkProtocolException($"Expected AC_ACCEPT_LOGIN3, got 0x{frame.PacketId:x4}.");
                }

                State = RoLoginSessionState.LoginAccepted;
                return RoLoginPacketParser.ParseAcAcceptLogin3(frame.Bytes, textEncoding);
            }
        }

        private RoInitialCharacterSnapshot LoadCharacters(RoAcAcceptLogin3 login, RoLoginCharServerInfo selectedServer) {
            RoServerEndpoint endpoint = ResolveCharacterEndpoint(selectedServer);
            using (RoTcpPacketConnection connection = RoTcpPacketConnection.Connect(
                endpoint,
                registry,
                profile,
                TraceSink,
                transform,
                TimeoutMilliseconds,
                captureSink,
                enableVerboseTraceMessages)) {
                return EnterCharacterServer(connection, login);
            }
        }

        private RoInitialCharacterSnapshot EnterCharacterServer(RoTcpPacketConnection connection, RoAcAcceptLogin3 login) {
            State = RoLoginSessionState.CharacterConnected;
            connection.Send(outboundPackets.BuildChEnter(login.AccountId, login.LoginId1, login.LoginId2, login.Sex));
            RoCharServerAccountAck accountAck = RoCharacterPacketParser.ParseCharServerAccountAck(connection.ReadRaw(4, "CHAR_SERVER_ACCOUNT_ACK"));
            if (accountAck.AccountId != login.AccountId) {
                throw new RoNetworkProtocolException($"Char-server account ack {accountAck.AccountId} does not match login account {login.AccountId}.");
            }

            bool hasSlotLimits = false;
            bool hasCharacterList = false;
            bool hasPageInfo = false;
            bool hasBlockedCharacters = false;
            bool hasSecondPassword = false;
            bool requireClassicSlotLimits = profile.PacketVersion == 20200401;

            RoHcAcceptEnter2 slotLimits = default;
            RoHcAcceptEnter characterList = default;
            RoHcNotifyCharList pageInfo = default;
            RoHcBlockCharacter blockedCharacters = default;
            RoHcSecondPasswordLogin secondPassword = default;

            while (!hasCharacterList ||
                   (requireClassicSlotLimits && !hasSlotLimits)) {
                RoPacketFrame frame = connection.ReadFrame();
                switch (frame.PacketId) {
                    case RoPacketIds.HcAcceptEnter2:
                        slotLimits = RoCharacterPacketParser.ParseHcAcceptEnter2(frame.Bytes, textEncoding);
                        hasSlotLimits = true;
                        break;
                    case RoPacketIds.HcAcceptEnter:
                        characterList = ParseHcAcceptEnterForProfile(frame.Bytes);
                        hasCharacterList = true;
                        break;
                    case RoPacketIds.HcAcceptEnter20240619:
                        characterList = RoCharacterPacketParser.ParseHcAcceptEnter20240619(frame.Bytes, textEncoding);
                        hasCharacterList = true;
                        break;
                    case RoPacketIds.HcAckCharInfoPerPage:
                        RoHcAckCharInfoPerPage page = profile.PacketVersion == 20240619
                            ? RoCharacterPacketParser.ParseHcAckCharInfoPerPage20240619(frame.Bytes, textEncoding)
                            : RoCharacterPacketParser.ParseHcAckCharInfoPerPage(frame.Bytes, textEncoding);
                        characterList = new RoHcAcceptEnter(page.PacketLength, 0, 0, 0, string.Empty, page.Characters);
                        hasCharacterList = true;
                        break;
                    case RoPacketIds.HcNotifyCharList:
                        pageInfo = RoCharacterPacketParser.ParseHcNotifyCharList(frame.Bytes);
                        hasPageInfo = true;
                        break;
                    case RoPacketIds.HcBlockCharacter:
                        blockedCharacters = RoCharacterPacketParser.ParseHcBlockCharacter(frame.Bytes, textEncoding);
                        hasBlockedCharacters = true;
                        break;
                    case RoPacketIds.HcSecondPasswordLogin:
                        secondPassword = RoCharacterPacketParser.ParseHcSecondPasswordLogin(frame.Bytes);
                        hasSecondPassword = true;
                        break;
                    default:
                        throw new RoNetworkProtocolException($"Unexpected char-server packet 0x{frame.PacketId:x4}.");
                }
            }

            DrainOptionalCharacterServerPrelude(
                connection,
                ref slotLimits,
                ref hasSlotLimits,
                ref characterList,
                ref hasCharacterList,
                ref pageInfo,
                ref hasPageInfo,
                ref blockedCharacters,
                ref hasBlockedCharacters,
                ref secondPassword,
                ref hasSecondPassword);

            if (!hasPageInfo) {
                pageInfo = new RoHcNotifyCharList(ResolveCharacterSlotCount(characterList, slotLimits));
            }

            return new RoInitialCharacterSnapshot(accountAck, slotLimits, characterList, pageInfo, blockedCharacters, secondPassword);
        }

        private static int ResolveCharacterSlotCount(
            RoHcAcceptEnter characterList,
            RoHcAcceptEnter2 slotLimits) {
            if (characterList.TotalSlots > 0) {
                return characterList.TotalSlots;
            }

            if (slotLimits.TotalSlots > 0) {
                return slotLimits.TotalSlots;
            }

            return characterList.Characters.Length;
        }

        private void DrainOptionalCharacterServerPrelude(
            RoTcpPacketConnection connection,
            ref RoHcAcceptEnter2 slotLimits,
            ref bool hasSlotLimits,
            ref RoHcAcceptEnter characterList,
            ref bool hasCharacterList,
            ref RoHcNotifyCharList pageInfo,
            ref bool hasPageInfo,
            ref RoHcBlockCharacter blockedCharacters,
            ref bool hasBlockedCharacters,
            ref RoHcSecondPasswordLogin secondPassword,
            ref bool hasSecondPassword) {
            while (connection.WaitForAvailableData(25)) {
                RoPacketFrame frame = connection.ReadFrame();
                switch (frame.PacketId) {
                    case RoPacketIds.HcAcceptEnter2:
                        slotLimits = RoCharacterPacketParser.ParseHcAcceptEnter2(frame.Bytes, textEncoding);
                        hasSlotLimits = true;
                        break;
                    case RoPacketIds.HcAcceptEnter:
                    case RoPacketIds.HcAcceptEnter20240619:
                    case RoPacketIds.HcAckCharInfoPerPage:
                        characterList = ParseCharacterListFrame(frame);
                        hasCharacterList = true;
                        break;
                    case RoPacketIds.HcNotifyCharList:
                        pageInfo = RoCharacterPacketParser.ParseHcNotifyCharList(frame.Bytes);
                        hasPageInfo = true;
                        break;
                    case RoPacketIds.HcBlockCharacter:
                        blockedCharacters = RoCharacterPacketParser.ParseHcBlockCharacter(frame.Bytes, textEncoding);
                        hasBlockedCharacters = true;
                        break;
                    case RoPacketIds.HcSecondPasswordLogin:
                        secondPassword = RoCharacterPacketParser.ParseHcSecondPasswordLogin(frame.Bytes);
                        hasSecondPassword = true;
                        break;
                    default:
                        throw new RoNetworkProtocolException($"Unexpected char-server prelude packet 0x{frame.PacketId:x4}.");
                }
            }
        }

        private RoHcAcceptEnter ParseCharacterListFrame(RoPacketFrame frame) {
            switch (frame.PacketId) {
                case RoPacketIds.HcAcceptEnter:
                    return ParseHcAcceptEnterForProfile(frame.Bytes);
                case RoPacketIds.HcAcceptEnter20240619:
                    return RoCharacterPacketParser.ParseHcAcceptEnter20240619(frame.Bytes, textEncoding);
                case RoPacketIds.HcAckCharInfoPerPage: {
                    RoHcAckCharInfoPerPage page = profile.PacketVersion == 20240619
                        ? RoCharacterPacketParser.ParseHcAckCharInfoPerPage20240619(frame.Bytes, textEncoding)
                        : RoCharacterPacketParser.ParseHcAckCharInfoPerPage(frame.Bytes, textEncoding);
                    return new RoHcAcceptEnter(page.PacketLength, 0, 0, 0, string.Empty, page.Characters);
                }
                default:
                    throw new ArgumentException($"Packet 0x{frame.PacketId:x4} is not a character list packet.", nameof(frame));
            }
        }

        private RoHcAcceptEnter ParseHcAcceptEnterForProfile(byte[] frame) {
            return profile.PacketVersion == 20240619
                ? RoCharacterPacketParser.ParseHcAcceptEnter20240619Legacy(frame, textEncoding)
                : RoCharacterPacketParser.ParseHcAcceptEnter(frame, textEncoding);
        }

        private RoHcAcceptMakeChar CreateCharacter(
                RoTcpPacketConnection connection,
                string characterName,
                int characterSlot,
                ushort hairColor,
                ushort hairStyle,
                int startJob,
                byte sex) {
                connection.Send(outboundPackets.BuildChMakeChar(
                    characterName,
                    characterSlot,
                    hairColor,
                    hairStyle,
                    startJob,
                    sex,
                    textEncoding));
                bool sawPassedSecondPassword = false;

                for (int attempt = 0; attempt < 8; attempt++) {
                    int waitMilliseconds = attempt == 0
                        ? TimeoutMilliseconds
                        : sawPassedSecondPassword ? 250 : TimeoutMilliseconds;
                    if (!connection.WaitForAvailableData(waitMilliseconds)) {
                        break;
                    }

                    RoPacketFrame frame = connection.ReadFrame();
                    if (frame.PacketId == RoPacketIds.HcRefuseMakeChar) {
                        RoHcRefuseMakeChar refuse = RoCharacterPacketParser.ParseHcRefuseMakeChar(frame.Bytes);
                        throw new RoNetworkProtocolException($"Character creation refused with error code {refuse.ErrorCode}.");
                    }

                    if (frame.PacketId == RoPacketIds.HcSecondPasswordLogin) {
                        RoHcSecondPasswordLogin secondPassword = RoCharacterPacketParser.ParseHcSecondPasswordLogin(frame.Bytes);
                        if (!secondPassword.IsPassedOrDisabled) {
                            throw new RoNetworkProtocolException(
                                $"Character creation requires unsupported second password result {secondPassword.Result}.");
                        }

                        sawPassedSecondPassword = true;
                        continue;
                    }

                    if (frame.PacketId == RoPacketIds.HcAcceptMakeChar ||
                        frame.PacketId == RoPacketIds.HcAcceptMakeChar20201007) {
                        State = RoLoginSessionState.CharacterCreated;
                        return NormalizeCreatedCharacter(
                            RoCharacterPacketParser.ParseHcAcceptMakeCharForProfile(frame.Bytes, textEncoding),
                            characterName,
                            characterSlot);
                    }

                    if (frame.PacketId == RoPacketIds.HcAcceptEnter ||
                        frame.PacketId == RoPacketIds.HcAcceptEnter20240619 ||
                        frame.PacketId == RoPacketIds.HcAckCharInfoPerPage) {
                        if (TryGetCreatedCharacterFromListFrame(frame, characterName, characterSlot, out RoHcAcceptMakeChar listedCharacter)) {
                            State = RoLoginSessionState.CharacterCreated;
                            return NormalizeCreatedCharacter(listedCharacter, characterName, characterSlot);
                        }

                        continue;
                    }

                    if (frame.PacketId == RoPacketIds.HcAcceptEnter2 ||
                        frame.PacketId == RoPacketIds.HcNotifyCharList ||
                        frame.PacketId == RoPacketIds.HcBlockCharacter) {
                        continue;
                    }

                    throw new RoNetworkProtocolException($"Expected HC_ACCEPT_MAKECHAR, got 0x{frame.PacketId:x4}.");
                }

                if (TryRefreshCreatedCharacter(connection, characterName, characterSlot, out RoHcAcceptMakeChar refreshedCharacter)) {
                    State = RoLoginSessionState.CharacterCreated;
                    return NormalizeCreatedCharacter(refreshedCharacter, characterName, characterSlot);
                }

                throw new RoNetworkProtocolException("Expected HC_ACCEPT_MAKECHAR after character creation request, but no create result arrived.");
            }

        private bool TryRefreshCreatedCharacter(
            RoTcpPacketConnection connection,
            string characterName,
            int characterSlot,
            out RoHcAcceptMakeChar createdCharacter) {
            createdCharacter = default;
            connection.Send(outboundPackets.BuildChCharListRequest());

            for (int attempt = 0; attempt < 4; attempt++) {
                if (!connection.WaitForAvailableData(TimeoutMilliseconds)) {
                    return false;
                }

                RoPacketFrame frame = connection.ReadFrame();
                if (TryGetCreatedCharacterFromListFrame(frame, characterName, characterSlot, out createdCharacter)) {
                    return true;
                }

                if (frame.PacketId == RoPacketIds.HcSecondPasswordLogin) {
                    RoHcSecondPasswordLogin secondPassword = RoCharacterPacketParser.ParseHcSecondPasswordLogin(frame.Bytes);
                    if (!secondPassword.IsPassedOrDisabled) {
                        throw new RoNetworkProtocolException(
                            $"Character creation refresh requires unsupported second password result {secondPassword.Result}.");
                    }

                    continue;
                }

                if (frame.PacketId == RoPacketIds.HcAcceptEnter2 ||
                    frame.PacketId == RoPacketIds.HcNotifyCharList ||
                    frame.PacketId == RoPacketIds.HcBlockCharacter) {
                    continue;
                }

                if (frame.PacketId == RoPacketIds.HcRefuseMakeChar) {
                    RoHcRefuseMakeChar refuse = RoCharacterPacketParser.ParseHcRefuseMakeChar(frame.Bytes);
                    throw new RoNetworkProtocolException($"Character creation refused with error code {refuse.ErrorCode}.");
                }

                throw new RoNetworkProtocolException($"Expected character list refresh after CH_CHARLIST_REQ, got 0x{frame.PacketId:x4}.");
            }

            return false;
        }

        private bool TryGetCreatedCharacterFromListFrame(
            RoPacketFrame frame,
            string characterName,
            int characterSlot,
            out RoHcAcceptMakeChar createdCharacter) {
            createdCharacter = default;
            if (frame.PacketId != RoPacketIds.HcAcceptEnter &&
                frame.PacketId != RoPacketIds.HcAcceptEnter20240619 &&
                frame.PacketId != RoPacketIds.HcAckCharInfoPerPage) {
                return false;
            }

            RoHcAcceptEnter list = ParseCharacterListFrame(frame);
            RoCharacterInfo character = FindCharacterByName(list.Characters, characterName);
            if (character == null) {
                return false;
            }

            createdCharacter = new RoHcAcceptMakeChar(character);
            return true;
        }

        private static RoHcAcceptMakeChar NormalizeCreatedCharacter(
            RoHcAcceptMakeChar createdCharacter,
            string requestedName,
            int requestedSlot) {
            RoCharacterInfo character = createdCharacter.Character;
            if (character == null) {
                return createdCharacter;
            }

            string normalizedName = (requestedName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(character.Name) && !string.IsNullOrWhiteSpace(normalizedName)) {
                character.Name = normalizedName;
            }

            if (character.Slot == 0 && requestedSlot > 0) {
                character.Slot = ClampCharacterSlot(requestedSlot);
            }

            return new RoHcAcceptMakeChar(character);
        }

        private static byte ClampCharacterSlot(int slot) {
            if (slot <= 0) {
                return 0;
            }

            return slot >= byte.MaxValue ? byte.MaxValue : (byte) slot;
        }
    
        private RoHcNotifyZoneServer2 SelectCharacter(RoTcpPacketConnection connection, int characterSlot) {
                connection.Send(outboundPackets.BuildChSelectChar(characterSlot));
                for (int attempt = 0; attempt < 4; attempt++) {
                    RoPacketFrame frame = connection.ReadFrame();
                    if (frame.PacketId == RoPacketIds.HcRefuseEnter) {
                        RoHcRefuseEnter refuse = RoCharacterPacketParser.ParseHcRefuseEnter(frame.Bytes);
                        throw new RoNetworkProtocolException($"Character select refused with error code {refuse.ErrorCode}.");
                    }

                    if (frame.PacketId == RoPacketIds.HcSecondPasswordLogin) {
                        RoHcSecondPasswordLogin secondPassword = RoCharacterPacketParser.ParseHcSecondPasswordLogin(frame.Bytes);
                        if (!secondPassword.IsPassedOrDisabled) {
                            throw new RoNetworkProtocolException(
                                $"Character select requires unsupported second password result {secondPassword.Result}.");
                        }

                        continue;
                    }
    
                    if (frame.PacketId == RoPacketIds.HcNotifyZoneServer ||
                        frame.PacketId == RoPacketIds.HcNotifyZoneServer2) {
                        return RoCharacterPacketParser.ParseHcNotifyZoneServer(frame.Bytes, textEncoding);
                    }
    
                    if (frame.PacketId == RoPacketIds.HcNotifyAccessibleMapName) {
                        RoHcNotifyAccessibleMapName accessibleMaps = RoCharacterPacketParser.ParseHcNotifyAccessibleMapName(frame.Bytes, textEncoding);
                        int mapIndex = accessibleMaps.FindFirstAvailableMapIndex();
                        if (mapIndex < 0) {
                            throw new RoNetworkProtocolException("Character select requested an accessible map, but none are available.");
                        }
    
                        connection.Send(outboundPackets.BuildChSelectAccessibleMapName(characterSlot, mapIndex));
                        continue;
                    }
    
                    throw new RoNetworkProtocolException($"Expected HC_NOTIFY_ZONESVR2, got 0x{frame.PacketId:x4}.");
                }
    
                throw new RoNetworkProtocolException("Character select did not return a map handoff after accessible map negotiation.");
            }
    
        private RoLoginEnterMapResult EnterMap(RoLoginCreateCharacterResult characterResult, bool acknowledgeLoadEnd) {
                return EnterMap(characterResult, acknowledgeLoadEnd, moveRequest: null);
            }
    
        private RoLoginEnterMapResult EnterMap(RoLoginCreateCharacterResult characterResult, bool acknowledgeLoadEnd, RoMapMoveRequest? moveRequest) {
                return EnterMap(characterResult, acknowledgeLoadEnd, moveRequest, keepMapConnectionAlive: false);
            }
    
        private RoLoginEnterMapResult EnterMap(
                RoLoginCreateCharacterResult characterResult,
                bool acknowledgeLoadEnd,
                RoMapMoveRequest? moveRequest,
                bool keepMapConnectionAlive,
                RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck = null) {
                RoHcNotifyZoneServer2 handoff = characterResult.MapHandoff;
                RoServerEndpoint endpoint = ResolveMapEndpoint(handoff);
    
                RoTcpPacketConnection connection = RoTcpPacketConnection.Connect(
                    endpoint,
                    registry,
                    profile,
                    TraceSink,
                    transform,
                    TimeoutMilliseconds,
                    captureSink,
                    enableVerboseTraceMessages);
                bool disposeConnection = true;
                try {
                    connection.Send(outboundPackets.BuildCzEnter2(
                        characterResult.Login.AccountId,
                        characterResult.SelectedCharacter.CharacterId,
                        characterResult.Login.LoginId1,
                        Environment.TickCount,
                        characterResult.Login.Sex));
    
                    bool hasAid = false;
                    bool hasAccept = false;
                    RoZcAid aid = default;
                    RoZcAcceptEnter2 acceptedEnter = default;
                    var preEntryPackets = new List<RoMapEntryDrainedPacket>();
                    var preEntryEvents = new List<RoMapEntryDrainEvent>();
    
                    while (!hasAid || !hasAccept) {
                        RoPacketFrame frame = connection.ReadFrame();
                        switch (frame.PacketId) {
                            case RoPacketIds.ZcAid:
                                aid = RoMapPacketParser.ParseZcAid(frame.Bytes);
                                hasAid = true;
                                break;
                            case RoPacketIds.ZcAcceptEnter:
                            case RoPacketIds.ZcAcceptEnter2:
                            case RoPacketIds.ZcAcceptEnter3:
                                acceptedEnter = RoMapPacketParser.ParseZcAcceptEnter(frame.Bytes);
                                hasAccept = true;
                                break;
                            case RoPacketIds.ZcRefuseEnter:
                                RoZcRefuseEnter refuse = RoMapPacketParser.ParseZcRefuseEnter(frame.Bytes);
                                throw new RoNetworkProtocolException($"Map entry refused with error code {refuse.ErrorCode}.");
                            case RoPacketIds.ScNotifyBan:
                                RoScNotifyBan ban = RoMapPacketParser.ParseScNotifyBan(frame.Bytes);
                                throw new RoNetworkProtocolException($"Server rejected map session with error code {ban.ErrorCode}.");
                            default:
                                if (IsSafeMapPreEntryPacket(frame.PacketId)) {
                                    preEntryPackets.Add(ToDrainedPacket(frame));
                                    AddSafePreEntryEvent(frame, preEntryEvents);
                                    break;
                                }
    
                                throw new RoNetworkProtocolException($"Unexpected map-server packet 0x{frame.PacketId:x4} before map entry completed.");
                        }
                    }
    
                    State = RoLoginSessionState.MapEntered;
                    if (Environment.GetEnvironmentVariable("RO_SEND_DEVICE_ID") == "1") SendDeviceIdentity(connection);
                    RoMapEntryDrainResult followUp = MergePreEntryPackets(preEntryPackets, preEntryEvents, DrainMapEntryFollowUps(connection));
                    RoMapEntryDrainResult initialState = RoMapEntryDrainResult.Empty;
                    RoMapEntryDrainResult moveResponse = RoMapEntryDrainResult.Empty;
    
                    if (acknowledgeLoadEnd) {
                        WaitForRuntimeMapLoadBeforeAck(loadRuntimeMapBeforeAck, handoff.MapName, acceptedEnter.Position.X, acceptedEnter.Position.Y);
                        connection.Send(outboundPackets.BuildCzNotifyActorInit());
                        initialState = DrainMapEntryFollowUps(connection, maxFrames: 96, idleMilliseconds: 150);
                        State = RoLoginSessionState.MapLoadAcknowledged;
                    }
    
                    if (moveRequest.HasValue) {
                        RoPacketPosition position = ResolveMoveRequest(moveRequest.Value, acceptedEnter.Position);
                        connection.Send(outboundPackets.BuildCzRequestMove2(position.X, position.Y, position.Direction));
                        moveResponse = DrainMapEntryFollowUps(connection, maxFrames: 24, idleMilliseconds: 750, stopAfterMovementResponse: true);
                        State = RoLoginSessionState.MapMoveRequested;
                    }
    
                    RoLiveMapSession mapSession = null;
                    if (keepMapConnectionAlive) {
                        mapSession = new RoLiveMapSession(this, connection, aid.AccountId);
                        disposeConnection = false;
                    }
    
                    return new RoLoginEnterMapResult(characterResult, aid, acceptedEnter, followUp, initialState, moveResponse, mapSession);
                } finally {
                    if (disposeConnection) {
                        connection.Dispose();
                    }
                }
            }
    
        internal RoMapEntryDrainResult DrainLiveMapSession(
                RoTcpPacketConnection connection,
                int maxFrames = 16,
                int idleMilliseconds = 100,
                bool stopAfterMovementResponse = false) {
                return DrainMapEntryFollowUps(connection, maxFrames, idleMilliseconds, stopAfterMovementResponse);
            }

        internal byte[] BuildCzNotifyActorInit() {
            return outboundPackets.BuildCzNotifyActorInit();
        }

        /// <summary>
        /// CZ_HWID once per map connection, where the profile has it (Custom Hercules 20240619). The original sends it 5 s
        /// after connecting to the map server and only for the first map connection of the process; the server accepts it
        /// before the load-end ack and keeps the first value of a session, so sending it on every map entry only closes the
        /// gap after a return to character select.
        /// </summary>
        private void SendDeviceIdentity(RoTcpPacketConnection connection) {
            if (!outboundPackets.TryResolveDefinition(RoOutboundPacketKind.CzHwid, out _) ||
                !RoDeviceIdentity.TryRead(out uint volumeSerial, out byte[] identifier)) {
                return;
            }

            connection.Send(outboundPackets.BuildCzHwid(volumeSerial, identifier));
        }

        internal void WaitForRuntimeMapLoadBeforeAck(RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck, string mapName, int spawnX, int spawnY) {
            if (loadRuntimeMapBeforeAck == null) {
                return;
            }

            Task loadTask = loadRuntimeMapBeforeAck(mapName, spawnX, spawnY);
            loadTask?.GetAwaiter().GetResult();
        }

        internal byte[] BuildCzRequestMove(int x, int y, int direction) {
            return outboundPackets.BuildCzRequestMove(x, y, direction);
        }

        internal byte[] BuildCzActionRequest(uint targetAid, byte actionType) {
            return outboundPackets.BuildCzActionRequest2(targetAid, actionType);
        }

        internal byte[] BuildCzRequestName(uint actorId) {
            return outboundPackets.BuildCzRequestName2(actorId);
        }

        internal byte[] BuildCzItemPickup(uint itemAid) {
            return outboundPackets.BuildCzItemPickup2(itemAid);
        }

        internal byte[] BuildCzUseSkill(int skillLevel, int skillId, uint targetAid) {
            return outboundPackets.BuildCzUseSkill2(skillLevel, skillId, targetAid);
        }

        internal byte[] BuildCzRestart(byte type) {
            return outboundPackets.BuildCzRestart(type);
        }

        internal byte[] BuildCzStandingResurrection() {
            return outboundPackets.BuildCzStandingResurrection();
        }
    
            internal RoLoginEnterMapResult CreateSameServerMapMoveResult(
                RoLoginEnterMapResult previousResult,
                RoZcNpcMapMove mapMove,
                RoMapEntryDrainResult initialState,
                RoLiveMapSession liveMapSession) {
                if (previousResult == null) {
                    throw new ArgumentNullException(nameof(previousResult));
                }
    
                RoLoginCreateCharacterResult characterResult = CreateMapChangeCharacterResult(
                    previousResult.CharacterResult,
                    mapMove.MapName,
                    previousResult.CharacterResult.MapHandoff.Address,
                    previousResult.CharacterResult.MapHandoff.Port,
                    previousResult.CharacterResult.MapHandoff.Domain);
                RoZcAcceptEnter2 acceptedEnter = new RoZcAcceptEnter2(
                    previousResult.AcceptedEnter.StartTime,
                    new RoPacketPosition(mapMove.X, mapMove.Y, previousResult.AcceptedEnter.Position.Direction),
                    previousResult.AcceptedEnter.XSize,
                    previousResult.AcceptedEnter.YSize,
                    previousResult.AcceptedEnter.Font);
    
                State = RoLoginSessionState.MapLoadAcknowledged;
                return new RoLoginEnterMapResult(
                    characterResult,
                    previousResult.Aid,
                    acceptedEnter,
                    RoMapEntryDrainResult.Empty,
                    initialState,
                    RoMapEntryDrainResult.Empty,
                    liveMapSession);
            }
    
            internal RoLoginEnterMapResult EnterRedirectedMap(
                RoLoginEnterMapResult previousResult,
                RoZcNpcServerMove serverMove,
                RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck = null) {
                if (previousResult == null) {
                    throw new ArgumentNullException(nameof(previousResult));
                }
    
                RoLoginCreateCharacterResult characterResult = CreateMapChangeCharacterResult(
                    previousResult.CharacterResult,
                    serverMove.MapName,
                    serverMove.Address,
                    serverMove.Port,
                    serverMove.Domain);
                return EnterMap(
                    characterResult,
                    acknowledgeLoadEnd: true,
                    moveRequest: null,
                    keepMapConnectionAlive: true,
                    loadRuntimeMapBeforeAck: loadRuntimeMapBeforeAck);
            }
    
        private RoMapEntryDrainResult DrainMapEntryFollowUps(
            RoTcpPacketConnection connection,
            int maxFrames = 16,
            int idleMilliseconds = 100,
            bool stopAfterMovementResponse = false) {
            using (BeginProcessBatchSample()) {
                const int SubsequentPacketIdleMilliseconds = 25;
                var accumulator = new RoMapEntryDrainAccumulator(textEncoding);
                bool stoppedOnUnsupportedPacket = false;
                string stopReason = string.Empty;

                while (accumulator.PacketCount < maxFrames && connection.WaitForAvailableData(
                           accumulator.PacketCount == 0 ? idleMilliseconds : Math.Min(idleMilliseconds, SubsequentPacketIdleMilliseconds))) {
                    RoPacketFrame frame;
                    try {
                        frame = connection.ReadFrame();
                    } catch (RoNetworkProtocolException ex) {
                        stoppedOnUnsupportedPacket = true;
                        stopReason = ex.Message;
                        break;
                    }

                    if (frame.PacketId == RoPacketIds.ScNotifyBan) {
                        throw new RoNetworkProtocolException(RoServerKick.Message(RoMapPacketParser.ParseScNotifyBan(frame.Bytes).ErrorCode));
                    }

                    if (frame.PacketId == RoPacketIds.ZcRestartAck && RoServerRestart.IsCharacterSelect(frame.Bytes)) {
                        throw new RoNetworkProtocolException(RoServerRestart.Message);
                    }

                    accumulator.AddFrame(frame);

                    if (stopAfterMovementResponse && accumulator.HasMovementResponse) {
                        break;
                    }
                }

                return accumulator.ToResult(stoppedOnUnsupportedPacket, stopReason);
            }
        }

        private static bool IsSafeMapPreEntryPacket(ushort packetId) {
            return packetId == RoPacketIds.ZcExtendBodyItemSize ||
                packetId == RoPacketIds.ZcNotifyWeightPercentage ||
                packetId == RoPacketIds.ZcAttackRange ||
                packetId == RoPacketIds.ZcCoupleStatus;
        }

        private static RoMapEntryDrainedPacket ToDrainedPacket(RoPacketFrame frame) {
            string packetName = string.IsNullOrWhiteSpace(frame.Definition.Name) ? $"0x{frame.PacketId:x4}" : frame.Definition.Name;
            return new RoMapEntryDrainedPacket(frame.PacketId, packetName, frame.Length, RoPacketFrameStatus.Complete);
        }

        private static void AddSafePreEntryEvent(RoPacketFrame frame, List<RoMapEntryDrainEvent> events) {
            if (events == null) {
                return;
            }

            switch (frame.PacketId) {
                case RoPacketIds.ZcNotifyWeightPercentage:
                    events.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyWeightPercentage(frame.Bytes)));
                    break;
                case RoPacketIds.ZcAttackRange:
                    events.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAttackRange(frame.Bytes)));
                    break;
                case RoPacketIds.ZcCoupleStatus:
                    events.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcCoupleStatus(frame.Bytes)));
                    break;
            }
        }

        private static RoMapEntryDrainResult MergePreEntryPackets(
            List<RoMapEntryDrainedPacket> preEntryPackets,
            List<RoMapEntryDrainEvent> preEntryEvents,
            RoMapEntryDrainResult followUp) {
            if ((preEntryPackets == null || preEntryPackets.Count == 0) &&
                (preEntryEvents == null || preEntryEvents.Count == 0)) {
                return followUp ?? RoMapEntryDrainResult.Empty;
            }

            followUp = followUp ?? RoMapEntryDrainResult.Empty;
            int preEntryPacketCount = preEntryPackets != null ? preEntryPackets.Count : 0;
            int preEntryEventCount = preEntryEvents != null ? preEntryEvents.Count : 0;

            var packets = new RoMapEntryDrainedPacket[preEntryPacketCount + followUp.Packets.Length];
            if (preEntryPacketCount > 0) {
                preEntryPackets.CopyTo(packets, 0);
            }

            Array.Copy(followUp.Packets, 0, packets, preEntryPacketCount, followUp.Packets.Length);

            var orderedEvents = new RoMapEntryDrainEvent[preEntryEventCount + followUp.OrderedEvents.Length];
            if (preEntryEventCount > 0) {
                preEntryEvents.CopyTo(orderedEvents, 0);
            }

            Array.Copy(followUp.OrderedEvents, 0, orderedEvents, preEntryEventCount, followUp.OrderedEvents.Length);

            return new RoMapEntryDrainResult(
                packets,
                followUp.PlayerChats,
                followUp.TimeNotifications,
                followUp.ActorEntries,
                followUp.ActorMoves,
                followUp.PlayerMoves,
                followUp.StopMoves,
                followUp.ActorVanishes,
                orderedEvents,
                followUp.StoppedOnUnsupportedPacket,
                followUp.StopReason,
                followUp.ParseFailureCount,
                followUp.ParseFailureMessages);
        }

        private static RoPacketPosition ResolveMoveRequest(RoMapMoveRequest requested, RoPacketPosition mapEntryPosition) {
            if (!requested.IsRelative) {
                if (requested.X < 0 || requested.Y < 0) {
                    throw new ArgumentOutOfRangeException(nameof(requested), "Move target coordinates must both be non-negative, or both negative for the default relative move.");
                }

                return new RoPacketPosition(requested.X, requested.Y, requested.Direction);
            }

            int direction = requested.Direction < 0 ? 6 : requested.Direction;
            return new RoPacketPosition(mapEntryPosition.X + 1, mapEntryPosition.Y, direction);
        }

        private readonly struct RoMapMoveRequest {
            public RoMapMoveRequest(int x, int y, int direction) {
                X = x;
                Y = y;
                Direction = direction;
            }

            public int X { get; }
            public int Y { get; }
            public int Direction { get; }
            public bool IsRelative => X < 0 && Y < 0;
        }

        private RoServerEndpoint ResolveCharacterEndpoint(RoLoginCharServerInfo selectedServer) {
            string host = string.IsNullOrWhiteSpace(selectedServer.Address) ? profile.Character.Host : selectedServer.Address;
            int port = selectedServer.Port == 0 ? profile.Character.Port : selectedServer.Port;
            return RoServerEndpoint.PreferProfileHostForLoopback(new RoServerEndpoint(host, port), profile.Character);
        }

        private RoServerEndpoint ResolveMapEndpoint(RoHcNotifyZoneServer2 handoff) {
            string host = !string.IsNullOrWhiteSpace(handoff.Domain)
                ? handoff.Domain
                : !string.IsNullOrWhiteSpace(handoff.Address) ? handoff.Address : profile.Map.Host;
            int port = handoff.Port == 0 ? profile.Map.Port : handoff.Port;
            return RoServerEndpoint.PreferProfileHostForLoopback(new RoServerEndpoint(host, port), profile.Map);
        }

        private RoLoginEnterMapResult EnterMapFromHandoff(
            int accountId,
            int loginId1,
            int loginId2,
            byte sex,
            int characterId,
            RoCharacterInfo selectedCharacter,
            RoHcNotifyZoneServer2 mapHandoff,
            RoInitialCharacterSnapshot initialCharacters,
            RoLoginCharServerInfo selectedCharacterServer,
            RoRuntimeMapLoadBeforeAckHandler loadRuntimeMapBeforeAck) {
            if (selectedCharacter == null) {
                throw new ArgumentNullException(nameof(selectedCharacter));
            }

            if (characterId <= 0) {
                throw new ArgumentOutOfRangeException(nameof(characterId), "Character id must be positive.");
            }

            if (selectedCharacter.CharacterId != 0 && selectedCharacter.CharacterId != characterId) {
                throw new ArgumentException("Selected character id must match the map handoff character id.", nameof(selectedCharacter));
            }

            if (mapHandoff.CharacterId != 0 && mapHandoff.CharacterId != characterId) {
                throw new ArgumentException("Map handoff character id must match the selected character id.", nameof(mapHandoff));
            }

            RoCharacterInfo character = CloneCharacterInfo(selectedCharacter);
            character.CharacterId = characterId;
            if (string.IsNullOrWhiteSpace(character.MapName)) {
                character.MapName = mapHandoff.MapName;
            }

            var login = new RoAcAcceptLogin3(
                loginId1,
                accountId,
                loginId2,
                lastIp: 0,
                lastLogin: string.Empty,
                sex,
                webAuthToken: string.Empty,
                Array.Empty<RoLoginCharServerInfo>());
            var characterResult = new RoLoginCreateCharacterResult(
                profile,
                login,
                selectedCharacterServer,
                initialCharacters ?? CreateSingleCharacterSnapshot(character),
                character,
                characterWasCreated: false,
                mapHandoff);
            return EnterMap(
                characterResult,
                acknowledgeLoadEnd: true,
                moveRequest: null,
                keepMapConnectionAlive: true,
                loadRuntimeMapBeforeAck: loadRuntimeMapBeforeAck);
        }

        private static RoInitialCharacterSnapshot CreateSingleCharacterSnapshot(RoCharacterInfo selectedCharacter) {
            if (selectedCharacter == null) {
                throw new ArgumentNullException(nameof(selectedCharacter));
            }

            return new RoInitialCharacterSnapshot(
                default,
                default,
                new RoHcAcceptEnter(0, 0, 0, 0, string.Empty, new[] { CloneCharacterInfo(selectedCharacter) }),
                default,
                default,
                default);
        }

        private static RoLoginCreateCharacterResult CreateMapChangeCharacterResult(
            RoLoginCreateCharacterResult previous,
            string mapName,
            string address,
            ushort port,
            string domain) {
            if (previous == null) {
                throw new ArgumentNullException(nameof(previous));
            }

            RoCharacterInfo character = CloneCharacterInfo(previous.SelectedCharacter);
            character.MapName = mapName ?? string.Empty;
            var handoff = new RoHcNotifyZoneServer2(
                character.CharacterId,
                character.MapName,
                address,
                port,
                domain);

            return new RoLoginCreateCharacterResult(
                previous.Profile,
                previous.Login,
                previous.SelectedCharacterServer,
                previous.InitialCharacters,
                character,
                previous.CharacterWasCreated,
                handoff);
        }

        internal static RoCharacterInfo CloneCharacterInfo(RoCharacterInfo source) {
            if (source == null) {
                throw new ArgumentNullException(nameof(source));
            }

            return new RoCharacterInfo {
                CharacterId = source.CharacterId,
                Experience = source.Experience,
                Money = source.Money,
                JobExperience = source.JobExperience,
                JobLevel = source.JobLevel,
                BodyState = source.BodyState,
                HealthState = source.HealthState,
                EffectState = source.EffectState,
                Virtue = source.Virtue,
                Honor = source.Honor,
                JobPoint = source.JobPoint,
                Hp = source.Hp,
                MaxHp = source.MaxHp,
                Sp = source.Sp,
                MaxSp = source.MaxSp,
                Speed = source.Speed,
                JobId = source.JobId,
                Head = source.Head,
                Body = source.Body,
                Weapon = source.Weapon,
                BaseLevel = source.BaseLevel,
                SkillPoint = source.SkillPoint,
                Accessory = source.Accessory,
                Shield = source.Shield,
                Accessory2 = source.Accessory2,
                Accessory3 = source.Accessory3,
                HeadPalette = source.HeadPalette,
                BodyPalette = source.BodyPalette,
                Name = source.Name,
                Strength = source.Strength,
                Agility = source.Agility,
                Vitality = source.Vitality,
                Intelligence = source.Intelligence,
                Dexterity = source.Dexterity,
                Luck = source.Luck,
                Slot = source.Slot,
                HairColor = source.HairColor,
                NameChangeFlag = source.NameChangeFlag,
                MapName = source.MapName,
                DeleteRevisionDate = source.DeleteRevisionDate,
                RobePalette = source.RobePalette,
                SlotChangeCount = source.SlotChangeCount,
                NameChangeCount = source.NameChangeCount,
                Sex = source.Sex,
            };
        }

        private static RoCharacterInfo FindCharacterByName(RoCharacterInfo[] characters, string characterName) {
            if (characters == null || string.IsNullOrWhiteSpace(characterName)) {
                return null;
            }

            for (int i = 0; i < characters.Length; i++) {
                RoCharacterInfo character = characters[i];
                if (character != null && string.Equals(character.Name, characterName, StringComparison.OrdinalIgnoreCase)) {
                    return character;
                }
            }

            return null;
        }

        private static RoCharacterInfo FindCharacterBySlot(RoCharacterInfo[] characters, int characterSlot) {
            if (characters == null) {
                return null;
            }

            for (int i = 0; i < characters.Length; i++) {
                RoCharacterInfo character = characters[i];
                if (character != null && character.Slot == characterSlot) {
                    return character;
                }
            }

            return null;
        }

        private static Encoding ResolveEncoding(int codePage) {
            try {
                return Encoding.GetEncoding(codePage);
            } catch (ArgumentException) {
                return Encoding.ASCII;
            } catch (NotSupportedException) {
                return Encoding.ASCII;
            }
        }
    }
}
