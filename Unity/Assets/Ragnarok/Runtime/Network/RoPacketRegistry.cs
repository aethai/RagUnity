using System;
using System.Collections.Generic;

namespace Ragnarok.Network {
    public readonly struct RoPacketDefinition : IEquatable<RoPacketDefinition> {
        public const int VariableLength = -1;

        public RoPacketDefinition(
            ushort packetId,
            string name,
            int length,
            RoPacketDirection direction,
            RoPacketFamily family) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Packet name is required.", nameof(name));
            }

            if (length != VariableLength && length < 2) {
                throw new ArgumentOutOfRangeException(nameof(length), length, "Fixed packet length must include the 2-byte id.");
            }

            PacketId = packetId;
            Name = name;
            Length = length;
            Direction = direction;
            Family = family;
        }

        public ushort PacketId { get; }
        public string Name { get; }
        public int Length { get; }
        public RoPacketDirection Direction { get; }
        public RoPacketFamily Family { get; }
        public bool IsVariableLength => Length == VariableLength;

        public bool Equals(RoPacketDefinition other) {
            return PacketId == other.PacketId &&
                Length == other.Length &&
                Direction == other.Direction &&
                Family == other.Family &&
                string.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) {
            return obj is RoPacketDefinition other && Equals(other);
        }

        public override int GetHashCode() {
            unchecked {
                int hash = PacketId.GetHashCode();
                hash = (hash * 397) ^ (Name == null ? 0 : Name.GetHashCode());
                hash = (hash * 397) ^ Length;
                hash = (hash * 397) ^ (int) Direction;
                hash = (hash * 397) ^ (int) Family;
                return hash;
            }
        }
    }

    public enum RoPacketFrameStatus {
        Complete = 0,
        NeedMoreData = 1,
        UnknownPacket = 2,
        InvalidLength = 3
    }

    public readonly struct RoPacketFrameMeasurement {
        public RoPacketFrameMeasurement(
            RoPacketFrameStatus status,
            ushort packetId,
            int frameLength,
            RoPacketDefinition definition) {
            Status = status;
            PacketId = packetId;
            FrameLength = frameLength;
            Definition = definition;
        }

        public RoPacketFrameStatus Status { get; }
        public ushort PacketId { get; }
        public int FrameLength { get; }
        public RoPacketDefinition Definition { get; }
        public bool IsComplete => Status == RoPacketFrameStatus.Complete;
    }

    public sealed class RoPacketRegistry {
        private readonly Dictionary<ushort, RoPacketDefinition> definitions;
        private readonly Dictionary<string, List<RoPacketDefinition>> definitionsByName;
        // the packetver whose server length table frames packets without a definition (0 = none)
        private readonly int fallbackLengthVersion;

        public RoPacketRegistry(IEnumerable<RoPacketDefinition> definitions, bool use20200401LengthFallbacks = false)
            : this(definitions, use20200401LengthFallbacks ? 20200401 : 0) {
        }

        public RoPacketRegistry(IEnumerable<RoPacketDefinition> definitions, int fallbackLengthVersion) {
            if (definitions == null) {
                throw new ArgumentNullException(nameof(definitions));
            }

            this.fallbackLengthVersion = fallbackLengthVersion;
            this.definitions = new Dictionary<ushort, RoPacketDefinition>();
            this.definitionsByName = new Dictionary<string, List<RoPacketDefinition>>(StringComparer.Ordinal);
            foreach (RoPacketDefinition definition in definitions) {
                if (this.definitions.ContainsKey(definition.PacketId)) {
                    throw new ArgumentException($"Duplicate packet id 0x{definition.PacketId:x4}.", nameof(definitions));
                }

                this.definitions.Add(definition.PacketId, definition);
                if (!definitionsByName.TryGetValue(definition.Name, out List<RoPacketDefinition> byName)) {
                    byName = new List<RoPacketDefinition>();
                    definitionsByName.Add(definition.Name, byName);
                }

                byName.Add(definition);
            }
        }

        public int Count => definitions.Count;
        public IEnumerable<RoPacketDefinition> Definitions => definitions.Values;

        public bool TryGet(ushort packetId, out RoPacketDefinition definition) {
            return definitions.TryGetValue(packetId, out definition);
        }

        public bool TryGet(string packetName, out RoPacketDefinition definition) {
            return TryGet(packetName, RoPacketDirection.Unknown, out definition);
        }

        public bool TryGet(string packetName, RoPacketDirection direction, out RoPacketDefinition definition) {
            if (string.IsNullOrWhiteSpace(packetName) ||
                !definitionsByName.TryGetValue(packetName, out List<RoPacketDefinition> candidates)) {
                definition = default;
                return false;
            }

            for (int i = 0; i < candidates.Count; i++) {
                RoPacketDefinition candidate = candidates[i];
                if (direction == RoPacketDirection.Unknown ||
                    candidate.Direction == RoPacketDirection.Unknown ||
                    candidate.Direction == direction) {
                    definition = candidate;
                    return true;
                }
            }

            definition = default;
            return false;
        }

        public bool TryResolveFrameDefinition(ushort packetId, out RoPacketDefinition definition) {
            if (definitions.TryGetValue(packetId, out definition)) {
                return true;
            }

            return TryCreateFallbackDefinition(packetId, out definition);
        }

        public bool TryResolveFrameDefinition(ushort packetId, RoPacketDirection direction, out RoPacketDefinition definition) {
            if (definitions.TryGetValue(packetId, out definition) &&
                (direction == RoPacketDirection.Unknown ||
                 definition.Direction == RoPacketDirection.Unknown ||
                 definition.Direction == direction)) {
                return true;
            }

            return TryCreateFallbackDefinition(packetId, out definition);
        }

        public RoPacketFrameMeasurement MeasureFrame(byte[] buffer, int offset, int count) {
            return MeasureFrame(buffer, offset, count, RoPacketDirection.Unknown);
        }

        public RoPacketFrameMeasurement MeasureFrame(byte[] buffer, int offset, int count, RoPacketDirection direction) {
            if (buffer == null) {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || offset + count > buffer.Length) {
                throw new ArgumentOutOfRangeException(nameof(count), "Frame range must fit inside the supplied buffer.");
            }

            if (count < 2) {
                return new RoPacketFrameMeasurement(RoPacketFrameStatus.NeedMoreData, 0, 0, default);
            }

            ushort packetId = (ushort) (buffer[offset] | (buffer[offset + 1] << 8));
            if (!TryResolveFrameDefinition(packetId, direction, out RoPacketDefinition definition)) {
                return new RoPacketFrameMeasurement(RoPacketFrameStatus.UnknownPacket, packetId, 0, default);
            }

            if (!definition.IsVariableLength) {
                if (count < definition.Length) {
                    return new RoPacketFrameMeasurement(RoPacketFrameStatus.NeedMoreData, packetId, definition.Length, definition);
                }

                return new RoPacketFrameMeasurement(RoPacketFrameStatus.Complete, packetId, definition.Length, definition);
            }

            if (count < 4) {
                return new RoPacketFrameMeasurement(RoPacketFrameStatus.NeedMoreData, packetId, 0, definition);
            }

            int frameLength = buffer[offset + 2] | (buffer[offset + 3] << 8);
            if (frameLength < 4) {
                return new RoPacketFrameMeasurement(RoPacketFrameStatus.InvalidLength, packetId, frameLength, definition);
            }

            if (count < frameLength) {
                return new RoPacketFrameMeasurement(RoPacketFrameStatus.NeedMoreData, packetId, frameLength, definition);
            }

            return new RoPacketFrameMeasurement(RoPacketFrameStatus.Complete, packetId, frameLength, definition);
        }

        private bool TryCreateFallbackDefinition(ushort packetId, out RoPacketDefinition definition) {
            int length = 0;
            bool known = fallbackLengthVersion == 20200401 ? RoPacketLengthTable20200401.TryGetLength(packetId, out length)
                : fallbackLengthVersion == 20240619 && RoPacketLengthTable20240619.TryGetLength(packetId, out length);
            if (!known || (length != RoPacketDefinition.VariableLength && length < 2)) {
                definition = default;
                return false;
            }

            string name = RoPacketNameTable20200401.TryGet(packetId, out string knownName, out RoPacketFamily knownFamily)
                ? $"UNHANDLED_{knownName}"
                : $"UNHANDLED_0x{packetId:x4}";

            definition = new RoPacketDefinition(
                packetId,
                name,
                length,
                RoPacketDirection.Unknown,
                knownFamily);
            return true;
        }

        public static RoPacketRegistry CreateLocalRathena20200401() {
            return new RoPacketRegistry(new[] {
                new RoPacketDefinition(RoPacketIds.CaLogin, "CA_LOGIN", 55, RoPacketDirection.ClientToServer, RoPacketFamily.Ca),
                new RoPacketDefinition(RoPacketIds.AcAcceptLogin3, "AC_ACCEPT_LOGIN3", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Ac),
                new RoPacketDefinition(RoPacketIds.AcRefuseLoginR2, "AC_REFUSE_LOGIN_R2", 26, RoPacketDirection.ServerToClient, RoPacketFamily.Ac),
                new RoPacketDefinition(RoPacketIds.ScNotifyBan, "SC_NOTIFY_BAN", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Sc),

                new RoPacketDefinition(RoPacketIds.ChEnter, "CH_ENTER", 17, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.ChSelectChar, "CH_SELECT_CHAR", 3, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.ChAvailableSecondPassword, "CH_AVAILABLE_SECOND_PASSWD", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.ChCharListRequest, "CH_CHARLIST_REQ", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.ChMakeChar, "CH_MAKE_CHAR", 36, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.ChDeleteChar3Reserved, "CH_DELETE_CHAR3_RESERVED", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.ChDeleteChar3, "CH_DELETE_CHAR3", 12, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.ChDeleteChar3Cancel, "CH_DELETE_CHAR3_CANCEL", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.ChDeleteChar2, "CH_DELETE_CHAR2", 56, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.ChSelectAccessibleMapName, "CH_SELECT_ACCESSIBLE_MAPNAME", 4, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.HcRefuseEnter, "HC_REFUSE_ENTER", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcAcceptMakeChar, "HC_ACCEPT_MAKECHAR", 157, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcRefuseMakeChar, "HC_REFUSE_MAKECHAR", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcBlockCharacter, "HC_BLOCK_CHARACTER", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcAcceptEnter, "HC_ACCEPT_ENTER", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcAcceptEnter2, "HC_ACCEPT_ENTER2", 29, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcDeleteChar3Reserved, "HC_DELETE_CHAR3_RESERVED", 14, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcDeleteChar3, "HC_DELETE_CHAR3", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcDeleteChar3Cancel, "HC_DELETE_CHAR3_CANCEL", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcAcceptDeleteChar, "HC_ACCEPT_DELETECHAR", 2, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcRefuseDeleteChar, "HC_REFUSE_DELETECHAR", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcSecondPasswordLogin, "HC_SECOND_PASSWD_LOGIN", 12, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcAckCharInfoPerPage, "HC_ACK_CHARINFO_PER_PAGE", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcNotifyCharList, "HC_NOTIFY_CHARLIST", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcNotifyAccessibleMapName, "HC_NOTIFY_ACCESSIBLE_MAPNAME", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcNotifyZoneServer2, "HC_NOTIFY_ZONESVR2", 156, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),

                new RoPacketDefinition(RoPacketIds.CzEnter2, "CZ_ENTER2", 19, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNotifyActorInit, "CZ_NOTIFY_ACTORINIT", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzRequestMove2, "CZ_REQUEST_MOVE2", 5, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzRequestTime2, "CZ_REQUEST_TIME", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzPingLive, "CZ_PING_LIVE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzActionRequest2, "CZ_REQUEST_ACT2", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzRequestName2, "CZ_REQUEST_NAME2", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqNameByGid, "CZ_REQNAME_BYGID", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzRestart, "CZ_RESTART", 3, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzStandingResurrection, "CZ_STANDING_RESURRECTION", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzContactNpc, "CZ_CONTACTNPC", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcSelectMenu, "CZ_NPC_SELECT_MENU", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqNextScript, "CZ_REQ_NEXT_SCRIPT", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzAckSelectDealType, "CZ_ACK_SELECT_DEALTYPE", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzPcPurchaseItemList, "CZ_PC_PURCHASE_ITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzPcSellItemList, "CZ_PC_SELL_ITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqCloseStore, "CZ_REQ_CLOSESTORE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqBuyFromMc, "CZ_REQ_BUY_FROMMC", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzPcPurchaseItemListFromMc, "CZ_PC_PURCHASE_ITEMLIST_FROMMC", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqOpenStore2, "CZ_REQ_OPENSTORE2", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzPcPurchaseItemListFromMc2, "CZ_PC_PURCHASE_ITEMLIST_FROMMC2", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcMarketPurchase, "CZ_NPC_MARKET_PURCHASE", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcMarketClose, "CZ_NPC_MARKET_CLOSE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcBarterMarketPurchase, "CZ_NPC_BARTER_MARKET_PURCHASE", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcBarterMarketClose, "CZ_NPC_BARTER_MARKET_CLOSE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcExpandedBarterMarketPurchase, "CZ_NPC_EXPANDED_BARTER_MARKET_PURCHASE", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcExpandedBarterMarketClose, "CZ_NPC_EXPANDED_BARTER_MARKET_CLOSE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzInputEditDlg, "CZ_INPUT_EDITDLG", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzCloseDialog, "CZ_CLOSE_DIALOG", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzInputEditDlgStr, "CZ_INPUT_EDITDLGSTR", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzWhisper, "CZ_WHISPER", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzRequestChat, "CZ_REQUEST_CHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzPartyChat, "CZ_PARTY_CHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqJoinGuild, "CZ_REQ_JOIN_GUILD", 14, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzJoinGuild, "CZ_JOIN_GUILD", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqLeaveGuild, "CZ_REQ_LEAVE_GUILD", 54, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzGuildChat, "CZ_GUILD_CHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzClanChat, "CZ_CLAN_CHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzCommandPet, "CZ_COMMAND_PET", 3, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzCloseStore, "CZ_CLOSE_STORE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzSelectWarpPoint, "CZ_SELECT_WARPPOINT", 20, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzItemThrow2, "CZ_ITEM_THROW2", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzMoveItemFromBodyToStore2, "CZ_MOVE_ITEM_FROM_BODY_TO_STORE2", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzMoveItemFromStoreToBody2, "CZ_MOVE_ITEM_FROM_STORE_TO_BODY2", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzMoveItemFromBodyToCart, "CZ_MOVE_ITEM_FROM_BODY_TO_CART", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzMoveItemFromCartToBody, "CZ_MOVE_ITEM_FROM_CART_TO_BODY", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzMoveItemFromStoreToCart, "CZ_MOVE_ITEM_FROM_STORE_TO_CART", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzMoveItemFromCartToStore, "CZ_MOVE_ITEM_FROM_CART_TO_STORE", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzCartOff, "CZ_CARTOFF", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqItemIdentify, "CZ_REQ_ITEMIDENTIFY", 4, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqItemCompositionList, "CZ_REQ_ITEMCOMPOSITION_LIST", 4, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqItemComposition, "CZ_REQ_ITEMCOMPOSITION", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqMakingArrow, "CZ_REQ_MAKINGARROW", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqChangeCart, "CZ_REQ_CHANGECART", 4, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzSelectCart, "CZ_SELECTCART", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqBankOpen, "CZ_REQ_BANK_OPEN", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqBankClose, "CZ_REQ_BANK_CLOSE", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqBankingCheck, "CZ_REQ_BANKING_CHECK", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqBankingDeposit, "CZ_REQ_BANKING_DEPOSIT", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqBankingWithdraw, "CZ_REQ_BANKING_WITHDRAW", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzOpenAllRodex, "CZ_OPEN_ALL_RODEX", 26, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzUpdateAllRodex, "CZ_UPDATE_ALL_RODEX", 26, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzCloseRodexBox, "CZ_CLOSE_RODEXBOX", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqReadRodex, "CZ_REQ_READ_RODEX", 11, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqNextRodex, "CZ_REQ_NEXT_RODEX", 11, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqDeleteRodex, "CZ_REQ_DELETE_RODEX", 11, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqZenyFromRodex, "CZ_REQ_ZENY_FROM_RODEX", 11, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqItemFromRodex, "CZ_REQ_ITEM_FROM_RODEX", 11, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqCancelWriteRodex, "CZ_REQ_CANCEL_WRITE_RODEX", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqAddItemRodex, "CZ_REQ_ADD_ITEM_RODEX", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqRemoveRodexItem, "CZ_REQ_REMOVE_RODEX_ITEM", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqOpenWriteRodex, "CZ_REQ_OPEN_WRITE_RODEX", 26, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzCheckReceiveCharacterName, "CZ_CHECK_RECEIVE_CHARACTER_NAME", 26, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzOpenUi, "CZ_OPEN_UI", 3, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqAttendance, "CZ_REQ_ATTENDANCE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzCheckRodexReceive, "CZ_CHECK_RODEX_RECEIVE", 27, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqSendRodex2, "CZ_REQ_SEND_RODEX2", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzRequestExchangeItem, "CZ_REQ_EXCHANGE_ITEM", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzAckExchangeItem, "CZ_ACK_EXCHANGE_ITEM", 3, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzAddExchangeItem, "CZ_ADD_EXCHANGE_ITEM", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzConcludeExchangeItem, "CZ_CONCLUDE_EXCHANGE_ITEM", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzCancelExchangeItem, "CZ_CANCEL_EXCHANGE_ITEM", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzExecExchangeItem, "CZ_EXEC_EXCHANGE_ITEM", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqTakeoffEquip, "CZ_REQ_TAKEOFF_EQUIP", 4, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzUseSkillToGround2, "CZ_USE_SKILL_TOGROUND2", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzUseSkill2, "CZ_USE_SKILL2", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzUseItem2, "CZ_USE_ITEM2", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzConfig, "CZ_CONFIG", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzEquipWinMicroscope, "CZ_EQUIPWIN_MICROSCOPE", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqWearEquipV5, "CZ_REQ_WEAR_EQUIP_V5", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzItemPickup2, "CZ_ITEM_PICKUP2", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcTradeQuit, "CZ_NPC_TRADE_QUIT", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzShortcutKeyChange2, "CZ_SHORTCUT_KEY_CHANGE2", 13, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzShortcutKeyBarRotate2, "CZ_SHORTCUTKEYBAR_ROTATE2", 5, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzStatusUp, "CZ_STATUS_UP", 5, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzUpgradeSkillLevel, "CZ_UPGRADE_SKILLLEVEL", 4, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzQuestActive, "CZ_ACTIVE_QUEST", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzMakeGroup2, "CZ_MAKE_GROUP2", 28, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqExpelGroupMember, "CZ_REQ_EXPEL_GROUP_MEMBER", 30, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzPartyJoinReq, "CZ_PARTY_JOIN_REQ", 26, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzPartyJoinReqAck, "CZ_PARTY_JOIN_REQ_ACK", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzLeaveGroup, "CZ_LEAVE_GROUP", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzGroupInfoChangeV2, "CZ_GROUPINFO_CHANGE_V2", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzChangeGroupMaster, "CZ_CHANGE_GROUP_MASTER", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzAddFriends, "CZ_ADD_FRIENDS", 26, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzDeleteFriends, "CZ_DELETE_FRIENDS", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzAckReqAddFriends, "CZ_ACK_REQ_ADD_FRIENDS", 14, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),

                new RoPacketDefinition(RoPacketIds.ZcRefuseEnter, "ZC_REFUSE_ENTER", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyTime, "ZC_NOTIFY_TIME", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyVanish, "ZC_NOTIFY_VANISH", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyChat, "ZC_NOTIFY_CHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyPlayerChat, "ZC_NOTIFY_PLAYERCHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcBroadcast, "ZC_BROADCAST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcBroadcast2, "ZC_BROADCAST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcChat, "ZC_NPC_CHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcWhisper, "ZC_WHISPER", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckWhisper, "ZC_ACK_WHISPER", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcWhisper2, "ZC_WHISPER2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckWhisper2, "ZC_ACK_WHISPER2", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMsg, "ZC_MSG", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcFormatStringMsg, "ZC_FORMATSTRING_MSG", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMsgValue, "ZC_MSG_VALUE", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMsgSkill, "ZC_MSG_SKILL", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMsgColor, "ZC_MSG_COLOR", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcFormatStringMsgColor, "ZC_FORMATSTRING_MSG_COLOR", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDebugMsg, "ZC_DEBUGMSG", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcAckMapMove, "ZC_NPCACK_MAPMOVE", 22, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcAckServerMove, "ZC_NPCACK_SERVERMOVE", 28, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSayDialog, "ZC_SAY_DIALOG", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcWaitDialog, "ZC_WAIT_DIALOG", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcCloseDialog, "ZC_CLOSE_DIALOG", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMenuList, "ZC_MENU_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSelectDealType, "ZC_SELECT_DEALTYPE", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseItemList, "ZC_PC_PURCHASE_ITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcSellItemList, "ZC_PC_SELL_ITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseResult, "ZC_PC_PURCHASE_RESULT", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcSellResult, "ZC_PC_SELL_RESULT", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcOpenStore, "ZC_OPENSTORE", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStoreEntry, "ZC_STORE_ENTRY", 86, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDisappearEntry, "ZC_DISAPPEAR_ENTRY", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseItemListFromMc, "ZC_PC_PURCHASE_ITEMLIST_FROMMC", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseResultFromMc, "ZC_PC_PURCHASE_RESULT_FROMMC", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseMyItemList, "ZC_PC_PURCHASE_MYITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDeleteItemFromMcStore, "ZC_DELETEITEM_FROM_MCSTORE", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcMarketOpen, "ZC_NPC_MARKET_OPEN", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcMarketPurchaseResult, "ZC_NPC_MARKET_PURCHASE_RESULT", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcMarketPurchaseResult2, "ZC_NPC_MARKET_PURCHASE_RESULT2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseItemList2, "ZC_PC_PURCHASE_ITEMLIST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseItemListFromMc2, "ZC_PC_PURCHASE_ITEMLIST_FROMMC2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDeleteItemFromMcStore2, "ZC_DELETEITEM_FROM_MCSTORE2", 18, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckOpenStore2, "ZC_ACK_OPENSTORE2", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseItemListFromMc3, "ZC_PC_PURCHASE_ITEMLIST_FROMMC3", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseMyItemList2, "ZC_PC_PURCHASE_MYITEMLIST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcBarterMarketItemInfo, "ZC_NPC_BARTER_MARKET_ITEMINFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcExpandedBarterMarketItemInfo, "ZC_NPC_EXPANDED_BARTER_MARKET_ITEMINFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcMarketOpen2, "ZC_NPC_MARKET_OPEN2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcOpenEditDlg, "ZC_OPEN_EDITDLG", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcShowImage, "ZC_SHOW_IMAGE", 19, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcOpenEditDlgStr, "ZC_OPEN_EDITDLGSTR", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcShowImage2, "ZC_SHOW_IMAGE2", 67, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcShowScript, "ZC_SHOWSCRIPT", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcClearDialog, "ZC_CLEAR_DIALOG", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStorageItemListNormal, "ZC_STORE_NORMAL_ITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStorageItemListEquip, "ZC_STORE_EQUIPMENT_ITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyStoreItemCountInfo, "ZC_NOTIFY_STOREITEM_COUNTINFO", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddItemToStore, "ZC_ADD_ITEM_TO_STORE", 21, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddItemToStore2, "ZC_ADD_ITEM_TO_STORE2", 22, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStorageItemListNormal2, "ZC_STORE_NORMAL_ITEMLIST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStorageItemListEquip2, "ZC_STORE_EQUIPMENT_ITEMLIST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStorageItemListEquip3, "ZC_STORE_EQUIPMENT_ITEMLIST3", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStorageItemListNormal3, "ZC_STORE_NORMAL_ITEMLIST3", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStorageItemListNormal4, "ZC_STORE_NORMAL_ITEMLIST4", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStorageItemListEquip4, "ZC_STORE_EQUIPMENT_ITEMLIST4", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStorageItemListEquip5, "ZC_STORE_EQUIPMENT_ITEMLIST5", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddItemToStore3, "ZC_ADD_ITEM_TO_STORE3", 57, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddItemToStore4, "ZC_ADD_ITEM_TO_STORE4", 58, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDeleteItemFromStore, "ZC_DELETE_ITEM_FROM_STORE", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcCloseStore, "ZC_CLOSE_STORE", 2, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcReqExchangeItem, "ZC_REQ_EXCHANGE_ITEM", 26, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckExchangeItem, "ZC_ACK_EXCHANGE_ITEM", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddExchangeItem, "ZC_ADD_EXCHANGE_ITEM", 19, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckAddExchangeItem, "ZC_ACK_ADD_EXCHANGE_ITEM", 5, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcConcludeExchangeItem, "ZC_CONCLUDE_EXCHANGE_ITEM", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcCancelExchangeItem, "ZC_CANCEL_EXCHANGE_ITEM", 2, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcExecExchangeItem, "ZC_EXEC_EXCHANGE_ITEM", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcReqExchangeItem2, "ZC_REQ_EXCHANGE_ITEM2", 32, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckExchangeItem2, "ZC_ACK_EXCHANGE_ITEM2", 9, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddExchangeItem2, "ZC_ADD_EXCHANGE_ITEM2", 20, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddExchangeItem3, "ZC_ADD_EXCHANGE_ITEM3", 45, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddExchangeItem4, "ZC_ADD_EXCHANGE_ITEM4", 61, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddExchangeItem5, "ZC_ADD_EXCHANGE_ITEM5", 62, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcCartOff, "ZC_CARTOFF", 2, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyAct, "ZC_NOTIFY_ACT", 29, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcChangeDirection, "ZC_CHANGE_DIRECTION", 9, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcItemEntry, "ZC_ITEM_ENTRY", 19, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcItemFallEntry, "ZC_ITEM_FALL_ENTRY", 17, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcItemDisappear, "ZC_ITEM_DISAPPEAR", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcUseItemAck, "ZC_USE_ITEM_ACK", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcUseItemAck2, "ZC_USE_ITEM_ACK2", 15, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcItemIdentifyList, "ZC_ITEMIDENTIFY_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckItemIdentify, "ZC_ACK_ITEMIDENTIFY", 5, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMakingArrowList, "ZC_MAKINGARROW_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSelectCart, "ZC_SELECTCART", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcWarpList, "ZC_WARPLIST", 68, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcWarpList2, "ZC_WARPLIST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyMapInfo, "ZC_NOTIFY_MAPINFO", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcItemCompositionList, "ZC_ITEMCOMPOSITION_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckItemComposition, "ZC_ACK_ITEMCOMPOSITION", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyCartItemCountInfo, "ZC_NOTIFY_CARTITEM_COUNTINFO", 14, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcEquipItemListCart, "ZC_CART_EQUIPMENT_ITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNonstackableItemListCart, "ZC_CART_NORMAL_ITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddItemToCart, "ZC_ADD_ITEM_TO_CART", 21, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDeleteItemFromCart, "ZC_DELETE_ITEM_FROM_CART", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddItemToCart2, "ZC_ADD_ITEM_TO_CART2", 22, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcEquipItemListCart2, "ZC_CART_EQUIPMENT_ITEMLIST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNonstackableItemListCart2, "ZC_CART_NORMAL_ITEMLIST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcCartNormalItemList4, "ZC_CART_NORMAL_ITEMLIST4", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcCartEquipItemList4, "ZC_CART_EQUIPMENT_ITEMLIST4", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddItemToCart3, "ZC_ADD_ITEM_TO_CART3", 57, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcCartEquipItemList5, "ZC_CART_EQUIPMENT_ITEMLIST5", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddItemToCart4, "ZC_ADD_ITEM_TO_CART4", 58, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcBankingCheck, "ZC_BANKING_CHECK", 12, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckBankingDeposit, "ZC_ACK_BANKING_DEPOSIT", 16, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckBankingWithdraw, "ZC_ACK_BANKING_WITHDRAW", 16, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckBankOpen, "ZC_ACK_OPEN_BANKING", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckBankClose, "ZC_ACK_CLOSE_BANKING", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcItemThrowAck, "ZC_ITEM_THROW_ACK", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcEmotion, "ZC_EMOTION", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqEmotion, "CZ_REQ_EMOTION", 3, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcRepairItemList, "ZC_REPAIRITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckItemRepair, "ZC_ACK_ITEMREPAIR", 5, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqItemRepair2, "CZ_REQ_ITEMREPAIR2", 26, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcNotifyWeaponItemList, "ZC_NOTIFY_WEAPONITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqWeaponRefine, "CZ_REQ_WEAPONREFINE", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcAckWeaponRefine, "ZC_ACK_WEAPONREFINE", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAutoSpellList, "ZC_AUTOSPELLLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzSelectAutoSpell, "CZ_SELECTAUTOSPELL", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcMergeItemOpen, "ZC_MERGE_ITEM_OPEN", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqMergeItem, "CZ_REQ_MERGE_ITEM", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcAckMergeItem, "ZC_ACK_MERGE_ITEM", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzCancelMergeItem, "CZ_CANCEL_MERGE_ITEM", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcMakableItemList, "ZC_MAKABLEITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqMakingItem, "CZ_REQMAKINGITEM", 18, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcAckReqMakingItem, "ZC_ACK_REQMAKINGITEM", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStartCapture, "ZC_START_CAPTURE", 2, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzTryCaptureMonster, "CZ_TRYCAPTURE_MONSTER", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcTryCaptureMonster, "ZC_TRYCAPTURE_MONSTER", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzPetEvolution, "CZ_PET_EVOLUTION", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcPetEvolutionResult, "ZC_PET_EVOLUTION_RESULT", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzRenamePet, "CZ_RENAME_PET", 26, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcPetEggList, "ZC_PETEGG_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzSelectPetEgg, "CZ_SELECT_PETEGG", 4, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcHomunculusProperty3, "ZC_PROPERTY_HOMUN_3", 73, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMercenaryParChange, "ZC_MER_PAR_CHANGE", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcHomunculusSkillUpdate, "ZC_HOSKILLINFO_UPDATE", 11, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzCommandMer, "CZ_COMMAND_MER", 5, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzRenameMer, "CZ_RENAME_MER", 26, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzMerCommand, "CZ_MER_COMMAND", 3, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqGuildMenuInterface, "CZ_REQ_GUILD_MENUINTERFACE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqGuildMenu, "CZ_REQ_GUILD_MENU", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqChangeMemberPos, "CZ_REQ_CHANGE_MEMBERPOS", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzRegChangeGuildPositionInfo, "CZ_REG_CHANGE_GUILD_POSITIONINFO", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcMyGuildBasicInfo, "ZC_MYGUILD_BASIC_INFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcGuildMenuInterface, "ZC_ACK_GUILD_MENUINTERFACE", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckReqChangeMembers, "ZC_ACK_REQ_CHANGE_MEMBERS", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPositionInfo, "ZC_POSITION_INFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPositionIdNameInfo, "ZC_POSITION_ID_NAME_INFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckChangeGuildPositionInfo, "ZC_ACK_CHANGE_GUILD_POSITIONINFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcUpdateCharStat2, "ZC_UPDATE_CHARSTAT2", 20, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcGuildAgitInfo, "ZC_GUILD_AGIT_INFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcGuildInfo3, "ZC_GUILD_INFO", 94, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMemberMgrInfo2, "ZC_MEMBERMGR_INFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqBanGuild, "CZ_REQ_BAN_GUILD", 54, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcAckBanGuild3, "ZC_ACK_BAN_GUILD3", 46, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckLeaveGuild2, "ZC_ACK_LEAVE_GUILD2", 46, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcGuildSkillInfo, "ZC_GUILD_SKILLINFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqDisorganizeGuild, "CZ_REQ_DISORGANIZE_GUILD", 42, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqMakeGuild, "CZ_REQ_MAKE_GUILD", 30, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzAllyGuild, "CZ_ALLY_GUILD", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzGuildNotice, "CZ_GUILD_NOTICE", 186, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcAckDisorganizeGuildResult, "ZC_ACK_DISORGANIZE_GUILD_RESULT", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcResultMakeGuild, "ZC_RESULT_MAKE_GUILD", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcReqAllyGuild, "ZC_REQ_ALLY_GUILD", 30, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckReqAllyGuild, "ZC_ACK_REQ_ALLY_GUILD", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckReqHostileGuild, "ZC_ACK_REQ_HOSTILE_GUILD", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDeleteRelatedGuild, "ZC_DELETE_RELATED_GUILD", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddRelatedGuild, "ZC_ADD_RELATED_GUILD", 34, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcBanList2, "ZC_BAN_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyMoveEntry11, "ZC_NOTIFY_MOVEENTRY11", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyNewEntry11, "ZC_NOTIFY_NEWENTRY11", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyStandEntry11, "ZC_NOTIFY_STANDENTRY11", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcInventoryListNormal4, "ZC_NORMAL_ITEMLIST4", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcInventoryListEquip4, "ZC_EQUIPMENT_ITEMLIST4", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcInventoryListEquip5, "ZC_EQUIPMENT_ITEMLIST5", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcInventoryStart, "ZC_INVENTORY_START", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcInventoryListNormal, "ZC_NORMAL_ITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcInventoryListEquip, "ZC_EQUIPMENT_ITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcInventoryListEquip2, "ZC_SPLIT_SEND_ITEMLIST_EQUIP2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcInventoryEnd, "ZC_INVENTORY_END", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDeleteItemFromBody, "ZC_DELETE_ITEM_FROM_BODY", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcEquipSwitchList, "ZC_EQUIPSWITCH_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckWearEquipV5, "ZC_ACK_WEAR_EQUIP_V5", 11, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcReqTakeoffEquipAck, "ZC_REQ_TAKEOFF_EQUIP_ACK", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcReqTakeoffEquipAck2, "ZC_REQ_TAKEOFF_EQUIP_ACK2", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckTakeoffEquipV5, "ZC_ACK_TAKEOFF_EQUIP_V5", 9, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcEquipArrow, "ZC_EQUIP_ARROW", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcParChange, "ZC_PAR_CHANGE", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcLongParChange, "ZC_LONGPAR_CHANGE", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcRecovery2, "ZC_RECOVERY2", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyExp2, "ZC_NOTIFY_EXP2", 18, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcLongLongParChange, "ZC_LONGLONGPAR_CHANGE", 12, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStatusChangeAck, "ZC_STATUS_CHANGE_ACK", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStatus, "ZC_STATUS", 44, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStatusChange, "ZC_STATUS_CHANGE", 5, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSpriteChange, "ZC_SPRITE_CHANGE", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSpriteChange2, "ZC_SPRITE_CHANGE2", 15, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckToUseSkill, "ZC_ACK_TOUSESKILL", 14, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifySkillLegacy, "ZC_NOTIFY_SKILL", 31, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifySkillPosition, "ZC_NOTIFY_SKILL_POSITION", 35, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyGroundSkill, "ZC_NOTIFY_GROUNDSKILL", 18, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSkillEntry, "ZC_SKILL_ENTRY", 16, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSkillEntry2, "ZC_SKILL_ENTRY2", 97, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSkillEntry3, "ZC_SKILL_ENTRY3", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSkillEntry4, "ZC_SKILL_ENTRY4", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStateChange, "ZC_STATE_CHANGE", 13, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSkillDisappear, "ZC_SKILL_DISAPPEAR", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcUseSkillAckLegacy, "ZC_USESKILL_ACK", 24, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAttackFailureForDistance, "ZC_ATTACK_FAILURE_FOR_DISTANCE", 16, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcActionFailure, "ZC_ACTION_FAILURE", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAttackRange, "ZC_ATTACK_RANGE", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDispel, "ZC_DISPEL", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyEffect2, "ZC_NOTIFY_EFFECT2", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyEffect3, "ZC_NOTIFY_EFFECT3", 14, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcShowEfstUpdate, "ZC_NPC_SHOWEFST_UPDATE", 18, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcCoupleStatus, "ZC_COUPLESTATUS", 14, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAutoRunSkill, "ZC_AUTORUN_SKILL", 39, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcResurrection, "ZC_RESURRECTION", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMsgStateChange, "ZC_MSG_STATE_CHANGE", 9, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyEffect, "ZC_NOTIFY_EFFECT", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifySkill2, "ZC_NOTIFY_SKILL2", 33, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckReqName, "ZC_ACK_REQNAME", 30, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckReqNameAll, "ZC_ACK_REQNAMEALL", 102, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcHighJump, "ZC_HIGHJUMP", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStateChange3, "ZC_STATE_CHANGE3", 15, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyAct2, "ZC_NOTIFY_ACT2", 33, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcReserved0306, "ZC_RESERVED_0306", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSkillPostDelay, "ZC_SKILL_POSTDELAY", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcUseSkillAck2, "ZC_USESKILL_ACK2", 25, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSkillEntry5, "ZC_SKILL_ENTRY5", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcUseSkill, "ZC_USE_SKILL", 17, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcUseSkillAck3, "ZC_USESKILL_ACK3", 29, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcShortcutKeyList, "ZC_SHORTCUT_KEY_LIST", 271, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSkillInfoUpdate, "ZC_SKILLINFO_UPDATE", 11, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddSkill, "ZC_ADD_SKILL", 39, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSkillInfoUpdate2, "ZC_SKILLINFO_UPDATE2", 15, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSkillInfoListLegacy, "ZC_SKILLINFO_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSkillInfoList, "ZC_SKILLINFO_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckReqNameAll2, "ZC_ACK_REQNAMEALL2", 106, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckReqNameAllNpc, "ZC_ACK_REQNAMEALL_NPC", 58, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyConfig, "ZC_PARTY_CONFIG", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyCreateAck, "ZC_ACK_MAKE_GROUP", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyInfoLegacy, "ZC_GROUP_LIST_LEGACY", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyInviteAck, "ZC_ACK_REQ_JOIN_GROUP", 27, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyInvite, "ZC_REQ_JOIN_GROUP", 30, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyOptionLegacy, "ZC_GROUPINFO_CHANGE", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyMemberInfoLegacy, "ZC_ADD_MEMBER_TO_GROUP_LEGACY", 79, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyMemberLeave, "ZC_DELETE_MEMBER_FROM_GROUP", 31, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyHpLegacy, "ZC_NOTIFY_HP_TO_GROUPM_LEGACY", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyOption, "ZC_REQ_GROUPINFO_CHANGE_V2", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyLeaderChanged, "ZC_CHANGE_GROUP_MASTER", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyMemberInfo2, "ZC_ADD_MEMBER_TO_GROUP2", 81, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyMemberInfo3, "ZC_ADD_MEMBER_TO_GROUP3", 85, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyInfo2, "ZC_GROUP_LIST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyMemberInfo, "ZC_ADD_MEMBER_TO_GROUP", 89, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyInfo, "ZC_GROUP_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyPosition, "ZC_NOTIFY_POSITION_TO_GROUPM", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcGuildPosition, "ZC_NOTIFY_POSITION_TO_GUILDM", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcQuestNotifyEffect, "ZC_QUEST_NOTIFY_EFFECT", 14, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcBossInfo, "ZC_BOSS_INFO", 70, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMinimapPing, "ZC_MINIMAP_PING", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyHp, "ZC_NOTIFY_HP_TO_GROUPM_R2", 14, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyAlive, "ZC_GROUP_ISALIVE", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPartyJobAndLevel, "ZC_NOTIFY_MEMBERINFO_TO_GROUPM", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcFriendsList, "ZC_FRIENDS_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcFriendState, "ZC_FRIENDS_STATE", 35, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcFriendRequest, "ZC_REQ_ADD_FRIENDS", 34, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcFriendAddResult, "ZC_ADD_FRIENDS_LIST", 36, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcFriendDelete, "ZC_DELETE_FRIENDS", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckReqNameByGid, "ZC_ACK_REQNAME_BYGID", 30, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzCreateChatRoom, "CZ_CREATE_CHATROOM", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcAckCreateChatRoom, "ZC_ACK_CREATE_CHATROOM", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcRoomNewEntry, "ZC_ROOM_NEWENTRY", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDestroyRoom, "ZC_DESTROY_ROOM", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqEnterRoom, "CZ_REQ_ENTER_ROOM", 14, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcRefuseEnterRoom, "ZC_REFUSE_ENTER_ROOM", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcEnterRoom, "ZC_ENTER_ROOM", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMemberNewEntry, "ZC_MEMBER_NEWENTRY", 28, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMemberExit, "ZC_MEMBER_EXIT", 29, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzChangeChatRoom, "CZ_CHANGE_CHATROOM", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcChangeChatRoom, "ZC_CHANGE_CHATROOM", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqRoleChange, "CZ_REQ_ROLE_CHANGE", 30, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcRoleChange, "ZC_ROLE_CHANGE", 30, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqExpelMember, "CZ_REQ_EXPEL_MEMBER", 26, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzExitRoom, "CZ_EXIT_ROOM", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcAckOpenBuyingStore, "ZC_ACK_OPEN_BUYING_STORE", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqOpenBuyingStore, "CZ_REQ_OPEN_BUYING_STORE", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcFailedOpenBuyingStore, "ZC_FAILED_OPEN_BUYING_STORE", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMyItemListBuyingStore, "ZC_MYITEMLIST_BUYING_STORE", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcBuyingStoreEntry, "ZC_BUYING_STORE_ENTRY", 86, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqCloseBuyingStore, "CZ_REQ_CLOSE_BUYING_STORE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcDisappearBuyingStoreEntry, "ZC_DISAPPEAR_BUYING_STORE_ENTRY", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqClickToBuyingStore, "CZ_REQ_CLICK_TO_BUYING_STORE", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcAckItemListBuyingStore, "ZC_ACK_ITEMLIST_BUYING_STORE", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzReqTradeBuyingStore, "CZ_REQ_TRADE_BUYING_STORE", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcFailedTradeBuyingStoreToBuyer, "ZC_FAILED_TRADE_BUYING_STORE_TO_BUYER", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDeleteItemFromBuyingStore, "ZC_DELETE_ITEM_FROM_BUYING_STORE", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcFailedTradeBuyingStoreToSeller, "ZC_FAILED_TRADE_BUYING_STORE_TO_SELLER", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcUpdateItemFromBuyingStore, "ZC_UPDATE_ITEM_FROM_BUYING_STORE", 24, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckReqNameByGid2, "ZC_ACK_REQNAME_BYGID2", 32, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcQuestDelete, "ZC_QUEST_DELETE", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcQuestObjectiveUpdateLegacy, "ZC_QUEST_OBJECTIVE_UPDATE_LEGACY", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcQuestActive, "ZC_ACTIVE_QUEST", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcQuestHuntInfo, "ZC_QUEST_HUNTINFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcQuestObjectiveUpdate, "ZC_QUEST_OBJECTIVE_UPDATE", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcQuestObjectiveUpdateModern, "ZC_QUEST_OBJECTIVE_UPDATE2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcQuestList, "ZC_QUEST_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcQuestAdd, "ZC_QUEST_ADD", 155, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcGuildLeaveAck, "ZC_ACK_LEAVE_GUILD", 66, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcGuildInviteResult, "ZC_ACK_REQ_JOIN_GUILD", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcGuildInvite, "ZC_REQ_JOIN_GUILD", 30, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcGuildBelongInfo, "ZC_UPDATE_GDID", 43, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcGuildNotice, "ZC_GUILD_NOTICE", 182, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcGuildMemberInfo, "ZC_ACK_GUILD_MEMBER_INFO", 106, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcGuildChat, "ZC_GUILD_CHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcClanChat, "ZC_NOTIFY_CLAN_CHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPetProperty, "ZC_PROPERTY_PET", 37, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcFeedPet, "ZC_FEED_PET", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPetChangeState, "ZC_CHANGESTATE_PET", 11, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPetAct, "ZC_PET_ACT", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcItemFallEntry2, "ZC_ITEM_FALL_ENTRY2", 19, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcHomunculusProperty, "ZC_PROPERTY_HOMUN_2", 77, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcHomunculusChangeState, "ZC_CHANGESTATE_MER", 12, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcHomunculusSkillInfoList, "ZC_HOSKILLINFO_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcHomunculusParChange, "ZC_HO_PAR_CHANGE", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMercenaryInfo, "ZC_MER_INIT", 80, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMercenarySkillInfoList, "ZC_MER_SKILLINFO_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcElementalInfo, "ZC_EL_INIT", 22, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcElementalParChange, "ZC_EL_PAR_CHANGE", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcConfig, "ZC_CONFIG", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcConfigNotify, "ZC_CONFIG_NOTIFY", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMsgStateChange2, "ZC_MSG_STATE_CHANGE2", 25, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyAct3, "ZC_NOTIFY_ACT3", 34, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMsgStateChangeTimed, "ZC_MSG_STATE_CHANGE_TIMED", 24, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMsgStateChange3, "ZC_MSG_STATE_CHANGE3", 29, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMsgStateChangeTimedWithTotal, "ZC_MSG_STATE_CHANGE_TIMED_TOTAL", 28, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcHpInfo, "ZC_HP_INFO", 14, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcExtendBodyItemSize, "ZC_EXTEND_BODYITEM_SIZE", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcUiOpen, "ZC_UI_OPEN", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAttendanceResponse, "ZC_ATTENDANCE_RESPONSE", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyUnreadMail, "ZC_NOTIFY_UNREADMAIL", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckReadRodex, "ZC_ACK_READ_RODEX", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckReadRodex2, "ZC_ACK_READ_RODEX2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckSendRodex, "ZC_ACK_SEND_RODEX", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckRodexList, "ZC_ACK_RODEX_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckRodexList2, "ZC_ACK_RODEX_LIST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckRodexList3, "ZC_ACK_RODEX_LIST3", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckRodexList4, "ZC_ACK_RODEX_LIST4", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckFailedAllRodexList, "ZC_ACK_FAILED_ALL_RODEX_LIST", 2, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckZenyFromRodex, "ZC_ACK_ZENY_FROM_RODEX", 12, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckItemFromRodex, "ZC_ACK_ITEM_FROM_RODEX", 12, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckDeleteRodex, "ZC_ACK_DELETE_RODEX", 11, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckAddItemRodex, "ZC_ACK_ADD_ITEM_RODEX", 63, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckAddItemRodex2, "ZC_ACK_ADD_ITEM_RODEX2", 64, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckRemoveRodexItem, "ZC_ACK_REMOVE_RODEX_ITEM", 9, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckOpenWriteRodex, "ZC_ACK_OPEN_WRITE_RODEX", 27, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcCheckReceiveCharacterName, "ZC_CHECK_RECEIVE_CHARACTER_NAME", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcCheckReceiveCharacterName2, "ZC_CHECK_RECEIVE_CHARACTER_NAME2", 34, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAllAchievementList, "ZC_ALL_ACH_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAchievementUpdate, "ZC_ACH_UPDATE", 66, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(0x0a26, "ZC_REQ_ACH_REWARD_ACK", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcItemPickupAck7, "ZC_ITEM_PICKUP_ACK7", 69, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcItemPickupAck8, "ZC_ITEM_PICKUP_ACK8", 70, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcChangeItemOption, "ZC_CHANGE_ITEM_OPTION", 48, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcItemFallEntry5, "ZC_ITEM_FALL_ENTRY5", 24, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyWeightPercentage, "ZC_NOTIFY_WEIGHT_PERCENTAGE", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyMapProperty, "ZC_NOTIFY_MAPPROPERTY", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyMapProperty2, "ZC_NOTIFY_MAPPROPERTY2", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcAckServerMoveDomain, "ZC_NPCACK_SERVERMOVE", 156, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAid, "ZC_AID", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAcceptEnter2, "ZC_ACCEPT_ENTER2", 13, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyMove, "ZC_NOTIFY_MOVE", 16, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyPlayerMove, "ZC_NOTIFY_PLAYERMOVE", 12, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStopMove, "ZC_STOPMOVE", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
            }, use20200401LengthFallbacks: true);
        }

        public static RoPacketRegistry CreateHercules20200401() {
            RoPacketRegistry local = CreateLocalRathena20200401();
            var definitions = new List<RoPacketDefinition>();
            foreach (RoPacketDefinition definition in local.Definitions) {
                if (string.Equals(definition.Name, "CZ_REQUEST_MOVE2", StringComparison.Ordinal)) {
                    continue;
                }

                definitions.Add(definition);
            }

            // Hercules' 20200401 main shuffle uses CZ_REQUEST_MOVE at 0x035f.
            // Keep the legacy profile's CZ_REQUEST_MOVE2 at 0x0881 for rAthena;
            // that opcode is a different packet in Hercules and would leave the
            // three position bytes in the server receive buffer.
            definitions.Add(new RoPacketDefinition(
                0x035f,
                "CZ_REQUEST_MOVE",
                5,
                RoPacketDirection.ClientToServer,
                RoPacketFamily.Cz));
            return new RoPacketRegistry(definitions, use20200401LengthFallbacks: true);
        }
    }
}
