using System;
using System.Collections.Generic;

namespace Ragnarok.Network {
    public interface IRoPacketProfile {
        string Name { get; }
        int PacketVersion { get; }
        int ClientVersion { get; }
        int LanguageType { get; }
        bool Renewal { get; }
        bool PacketObfuscation { get; }
        bool PacketCryptAvailable { get; }
        RoPacketProfileCapabilities Capabilities { get; }
        RoPacketRegistry CreateRegistry();
        IRoPacketTransform CreatePacketTransform();
    }

    public sealed class RoPacketProfileCapabilities {
        public static readonly RoPacketProfileCapabilities None = new RoPacketProfileCapabilities(
            supportsLoginRuntimeInbound: false,
            supportsCharRuntimeInbound: false,
            supportsMapRuntimeInbound: false,
            supportsReplayDecode: false,
            loginRuntimePacketIds: Array.Empty<ushort>(),
            charRuntimePacketIds: Array.Empty<ushort>(),
            mapRuntimePacketIds: Array.Empty<ushort>());

        public RoPacketProfileCapabilities(
            bool supportsLoginRuntimeInbound,
            bool supportsCharRuntimeInbound,
            bool supportsMapRuntimeInbound,
            bool supportsReplayDecode,
            ushort[] loginRuntimePacketIds,
            ushort[] charRuntimePacketIds,
            ushort[] mapRuntimePacketIds) {
            SupportsLoginRuntimeInbound = supportsLoginRuntimeInbound;
            SupportsCharRuntimeInbound = supportsCharRuntimeInbound;
            SupportsMapRuntimeInbound = supportsMapRuntimeInbound;
            SupportsReplayDecode = supportsReplayDecode;
            LoginRuntimePacketIds = loginRuntimePacketIds ?? Array.Empty<ushort>();
            CharRuntimePacketIds = charRuntimePacketIds ?? Array.Empty<ushort>();
            MapRuntimePacketIds = mapRuntimePacketIds ?? Array.Empty<ushort>();
        }

        public bool SupportsLoginRuntimeInbound { get; }
        public bool SupportsCharRuntimeInbound { get; }
        public bool SupportsMapRuntimeInbound { get; }
        public bool SupportsReplayDecode { get; }
        public ushort[] LoginRuntimePacketIds { get; }
        public ushort[] CharRuntimePacketIds { get; }
        public ushort[] MapRuntimePacketIds { get; }
    }

    public sealed class RoPacketProfileDescriptor : IRoPacketProfile {
        private readonly Func<RoPacketRegistry> registryFactory;
        private readonly Func<IRoPacketTransform> transformFactory;

        public RoPacketProfileDescriptor(
            string name,
            int packetVersion,
            int clientVersion,
            int languageType,
            bool renewal,
            bool packetObfuscation,
            bool packetCryptAvailable,
            Func<RoPacketRegistry> registryFactory,
            Func<IRoPacketTransform> transformFactory = null,
            RoPacketProfileCapabilities capabilities = null) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Packet profile name is required.", nameof(name));
            }

            if (packetVersion <= 0) {
                throw new ArgumentOutOfRangeException(nameof(packetVersion), packetVersion, "Packet version must be positive.");
            }

            if (clientVersion < 0) {
                throw new ArgumentOutOfRangeException(nameof(clientVersion), clientVersion, "Client version cannot be negative.");
            }

            if (languageType < 0) {
                throw new ArgumentOutOfRangeException(nameof(languageType), languageType, "Language type cannot be negative.");
            }

            Name = name;
            PacketVersion = packetVersion;
            ClientVersion = clientVersion;
            LanguageType = languageType;
            Renewal = renewal;
            PacketObfuscation = packetObfuscation;
            PacketCryptAvailable = packetCryptAvailable;
            this.registryFactory = registryFactory ?? throw new ArgumentNullException(nameof(registryFactory));
            this.transformFactory = transformFactory ?? (() => RoNoPacketTransform.Instance);
            Capabilities = capabilities ?? RoPacketProfileCapabilities.None;
        }

        public string Name { get; }
        public int PacketVersion { get; }
        public int ClientVersion { get; }
        public int LanguageType { get; }
        public bool Renewal { get; }
        public bool PacketObfuscation { get; }
        public bool PacketCryptAvailable { get; }
        public RoPacketProfileCapabilities Capabilities { get; }

        public RoPacketRegistry CreateRegistry() {
            RoPacketRegistry registry = registryFactory();
            if (registry == null) {
                throw new InvalidOperationException($"Packet profile '{Name}' returned a null registry.");
            }

            return registry;
        }

        public IRoPacketTransform CreatePacketTransform() {
            IRoPacketTransform transform = transformFactory();
            if (transform == null) {
                throw new InvalidOperationException($"Packet profile '{Name}' returned a null packet transform.");
            }

            return transform;
        }
    }

    public static class RoPacketProfiles {
        public static IRoPacketProfile LocalRathena20200401() {
            RoServerProfile profile = RoServerProfile.LocalRathena20200401();
            return new RoPacketProfileDescriptor(
                profile.Name,
                profile.PacketVersion,
                profile.ClientVersion,
                profile.LanguageType,
                profile.Renewal,
                profile.PacketObfuscation,
                packetCryptAvailable: false,
                registryFactory: RoPacketRegistry.CreateLocalRathena20200401,
                capabilities: Create20200401Capabilities());
        }

        public static IRoPacketProfile Hercules20200401() {
            RoServerProfile profile = RoServerProfile.LocalRathena20200401();
            return new RoPacketProfileDescriptor(
                "Hercules 20200401",
                profile.PacketVersion,
                profile.ClientVersion,
                profile.LanguageType,
                profile.Renewal,
                profile.PacketObfuscation,
                packetCryptAvailable: false,
                registryFactory: RoPacketRegistry.CreateHercules20200401,
                capabilities: Create20200401Capabilities());
        }

        public static IRoPacketProfile ForServerProfile(RoServerProfile serverProfile) {
            switch (serverProfile.PacketVersion) {
                case 20200401:
                    return serverProfile.Name != null &&
                        serverProfile.Name.IndexOf("Hercules", StringComparison.OrdinalIgnoreCase) >= 0
                        ? Hercules20200401()
                        : LocalRathena20200401();
                case 20240619 when serverProfile.Name != null &&
                                   serverProfile.Name.IndexOf("Hercules", StringComparison.OrdinalIgnoreCase) >= 0:
                    return Hercules20240619();
                case 20240619:
                    return serverProfile.PacketCryptConfig.Enabled
                        ? Rathena20240619Seed(serverProfile.PacketCryptConfig)
                        : Rathena20240619Seed();
                default:
                    throw new NotSupportedException(
                        $"No packet profile is registered for packetver {serverProfile.PacketVersion}.");
            }
        }

        /// <summary>
        /// Custom production: Hercules built with PACKETVER 20240619 (main). Its CZ shuffle is the unshuffled set (the
        /// block for 20221019+), its obfuscation keys are zero, and its length table frames what the seed does not define.
        /// </summary>
        public static IRoPacketProfile Hercules20240619() {
            return new RoPacketProfileDescriptor(
                "Hercules 20240619",
                packetVersion: 20240619,
                clientVersion: 45,
                languageType: 0,
                renewal: false,
                packetObfuscation: false,
                packetCryptAvailable: false,
                registryFactory: CreateHercules20240619Registry,
                capabilities: Create20240619Capabilities());
        }

        private static RoPacketRegistry CreateHercules20240619Registry() {
            var definitions = new List<RoPacketDefinition>();
            foreach (RoPacketDefinition definition in CreateRathena20240619SeedRegistry().Definitions) {
                // Hercules sends HC_ACCEPT_ENTER2 with a length word (packets2022_len_main.h: -1)
                definitions.Add(definition.PacketId == RoPacketIds.HcAcceptEnter2
                    ? new RoPacketDefinition(definition.PacketId, definition.Name, RoPacketDefinition.VariableLength, definition.Direction, definition.Family)
                    : definition);
            }

            // Custom's own device id (packets.h "// Custom", PACKETVER_MAIN_NUM >= 20240619); rAthena drops a session on it
            definitions.Add(new RoPacketDefinition(0x0be3, "CZ_HWID", 48, RoPacketDirection.ClientToServer, RoPacketFamily.Cz));
            // the server signature of a Custom release (RoServerSignature, the login server's server_sign plugin)
            definitions.Add(new RoPacketDefinition(RoServerSignature.RequestId, "CA_CUSTOM_SIGN_REQ", RoServerSignature.RequestLength,
                RoPacketDirection.ClientToServer, RoPacketFamily.Ca));
            definitions.Add(new RoPacketDefinition(RoServerSignature.ReplyId, "AC_CUSTOM_SIGN_ACK", RoServerSignature.ReplyLength,
                RoPacketDirection.ServerToClient, RoPacketFamily.Ac));
            return new RoPacketRegistry(definitions, 20240619);
        }

        public static IRoPacketProfile Rathena20240619Seed() {
            return new RoPacketProfileDescriptor(
                "Custom-derived rAthena 20240619 seed",
                packetVersion: 20240619,
                clientVersion: 45,
                languageType: 0,
                renewal: true,
                packetObfuscation: false,
                packetCryptAvailable: false,
                registryFactory: CreateRathena20240619SeedRegistry,
                capabilities: Create20240619Capabilities());
        }

        public static IRoPacketProfile Rathena20240619Seed(RoPacketCryptConfig cryptConfig) {
            if (!cryptConfig.Enabled) {
                throw new ArgumentException("20240619 packet crypt profile requires an enabled crypt config.", nameof(cryptConfig));
            }

            return new RoPacketProfileDescriptor(
                "Custom-derived rAthena 20240619 seed + packet crypt",
                packetVersion: 20240619,
                clientVersion: 45,
                languageType: 0,
                renewal: true,
                packetObfuscation: false,
                packetCryptAvailable: true,
                registryFactory: CreateRathena20240619SeedRegistry,
                transformFactory: () => new RoPacketCryptTransform(cryptConfig),
                capabilities: Create20240619Capabilities());
        }

        private static RoPacketProfileCapabilities Create20200401Capabilities() {
            return new RoPacketProfileCapabilities(
                supportsLoginRuntimeInbound: true,
                supportsCharRuntimeInbound: true,
                supportsMapRuntimeInbound: true,
                supportsReplayDecode: true,
                loginRuntimePacketIds: new[] {
                    RoPacketIds.AcAcceptLogin3,
                    RoPacketIds.AcRefuseLoginR2,
                    RoPacketIds.ScNotifyBan
                },
                charRuntimePacketIds: new[] {
                    RoPacketIds.HcAcceptEnter2,
                    RoPacketIds.HcAcceptEnter,
                    RoPacketIds.HcNotifyCharList,
                    RoPacketIds.HcBlockCharacter,
                    RoPacketIds.HcSecondPasswordLogin,
                    RoPacketIds.HcAcceptMakeChar,
                    RoPacketIds.HcRefuseMakeChar,
                    RoPacketIds.HcDeleteChar3Reserved,
                    RoPacketIds.HcDeleteChar3,
                    RoPacketIds.HcDeleteChar3Cancel,
                    RoPacketIds.HcAcceptDeleteChar,
                    RoPacketIds.HcRefuseDeleteChar,
                    RoPacketIds.HcNotifyZoneServer2,
                    RoPacketIds.HcRefuseEnter
                },
                mapRuntimePacketIds: new[] {
                    RoPacketIds.ZcAid,
                    RoPacketIds.ZcAcceptEnter2,
                    RoPacketIds.ZcRefuseEnter,
                    RoPacketIds.ZcOpenStore,
                    RoPacketIds.ZcStoreEntry,
                    RoPacketIds.ZcDisappearEntry,
                    RoPacketIds.ZcPcPurchaseItemListFromMc,
                    RoPacketIds.ZcPcPurchaseResultFromMc,
                    RoPacketIds.ZcPcPurchaseMyItemList,
                    RoPacketIds.ZcDeleteItemFromMcStore,
                    RoPacketIds.ZcPcPurchaseItemListFromMc2,
                    RoPacketIds.ZcDeleteItemFromMcStore2,
                    RoPacketIds.ZcAckOpenStore2,
                    RoPacketIds.ZcPcPurchaseItemListFromMc3,
                    RoPacketIds.ZcPcPurchaseMyItemList2,
                    RoPacketIds.ScNotifyBan
                });
        }

        private static RoPacketProfileCapabilities Create20240619Capabilities() {
            return new RoPacketProfileCapabilities(
                supportsLoginRuntimeInbound: true,
                supportsCharRuntimeInbound: true,
                supportsMapRuntimeInbound: true,
                supportsReplayDecode: true,
                loginRuntimePacketIds: new[] {
                    RoPacketIds.AcAcceptLogin3,
                    RoPacketIds.AcRefuseLoginR2,
                    RoPacketIds.AcRefuseLogin20240619,
                    RoPacketIds.ScNotifyBan
                },
                charRuntimePacketIds: new[] {
                    RoPacketIds.HcAcceptEnter2,
                    RoPacketIds.HcAcceptEnter20240619,
                    RoPacketIds.HcAckCharInfoPerPage,
                    RoPacketIds.HcNotifyCharList,
                    RoPacketIds.HcBlockCharacter,
                    RoPacketIds.HcSecondPasswordLogin,
                    RoPacketIds.HcAcceptMakeChar,
                    RoPacketIds.HcAcceptMakeChar20201007,
                    RoPacketIds.HcRefuseMakeChar,
                    RoPacketIds.HcDeleteChar3Reserved,
                    RoPacketIds.HcDeleteChar3,
                    RoPacketIds.HcDeleteChar3Cancel,
                    RoPacketIds.HcAcceptDeleteChar,
                    RoPacketIds.HcRefuseDeleteChar,
                    RoPacketIds.HcNotifyZoneServer,
                    RoPacketIds.HcNotifyZoneServer2,
                    RoPacketIds.HcRefuseEnter
                },
                mapRuntimePacketIds: new[] {
                    RoPacketIds.ZcAid,
                    RoPacketIds.ZcAcceptEnter,
                    RoPacketIds.ZcAcceptEnter2,
                    RoPacketIds.ZcAcceptEnter3,
                    RoPacketIds.ZcRefuseEnter,
                    RoPacketIds.ZcOpenStore,
                    RoPacketIds.ZcStoreEntry,
                    RoPacketIds.ZcDisappearEntry,
                    RoPacketIds.ZcPcPurchaseItemListFromMc,
                    RoPacketIds.ZcPcPurchaseResultFromMc,
                    RoPacketIds.ZcPcPurchaseMyItemList,
                    RoPacketIds.ZcDeleteItemFromMcStore,
                    RoPacketIds.ZcPcPurchaseItemListFromMc2,
                    RoPacketIds.ZcDeleteItemFromMcStore2,
                    RoPacketIds.ZcAckOpenStore2,
                    RoPacketIds.ZcPcPurchaseItemListFromMc3,
                    RoPacketIds.ZcPcPurchaseMyItemList2,
                    RoPacketIds.ScNotifyBan
                });
        }

        private static RoPacketRegistry CreateRathena20240619SeedRegistry() {
            var definitions = new List<RoPacketDefinition> {
                new RoPacketDefinition(0x0064, "CA_LOGIN", 55, RoPacketDirection.ClientToServer, RoPacketFamily.Ca),
                new RoPacketDefinition(RoPacketIds.AcAcceptLogin3, "AC_ACCEPT_LOGIN3", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Ac),
                new RoPacketDefinition(RoPacketIds.AcRefuseLoginR2, "AC_REFUSE_LOGIN_R2", 26, RoPacketDirection.ServerToClient, RoPacketFamily.Ac),
                new RoPacketDefinition(RoPacketIds.AcRefuseLogin20240619, "AC_REFUSE_LOGIN", 26, RoPacketDirection.ServerToClient, RoPacketFamily.Ac),
                new RoPacketDefinition(RoPacketIds.ScNotifyBan, "SC_NOTIFY_BAN", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Sc),

                new RoPacketDefinition(0x0065, "CH_ENTER", 17, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(0x0066, "CH_SELECT_CHAR", 3, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(0x09a1, "CH_CHARLIST_REQ", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(0x0a39, "CH_MAKE_CHAR", 36, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.ChDeleteChar3Reserved, "CH_DELETE_CHAR3_RESERVED", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.ChDeleteChar3, "CH_DELETE_CHAR3", 12, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.ChDeleteChar3Cancel, "CH_DELETE_CHAR3_CANCEL", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.ChDeleteChar2, "CH_DELETE_CHAR2", 56, RoPacketDirection.ClientToServer, RoPacketFamily.Ch),
                new RoPacketDefinition(RoPacketIds.HcRefuseEnter, "HC_REFUSE_ENTER", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcAcceptEnter, "HC_ACCEPT_ENTER", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcAcceptEnter2, "HC_ACCEPT_ENTER2", 29, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcSecondPasswordLogin, "HC_SECOND_PASSWD_LOGIN", 12, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcNotifyCharList, "HC_NOTIFY_CHARLIST", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcBlockCharacter, "HC_BLOCK_CHARACTER", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcAcceptMakeChar, "HC_ACCEPT_MAKECHAR", 157, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcAcceptMakeChar20201007, "HC_ACCEPT_MAKECHAR", 177, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcRefuseMakeChar, "HC_REFUSE_MAKECHAR", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcDeleteChar3Reserved, "HC_DELETE_CHAR3_RESERVED", 14, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcDeleteChar3, "HC_DELETE_CHAR3", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcDeleteChar3Cancel, "HC_DELETE_CHAR3_CANCEL", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcAcceptDeleteChar, "HC_ACCEPT_DELETECHAR", 2, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcRefuseDeleteChar, "HC_REFUSE_DELETECHAR", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcAckCharInfoPerPage, "HC_ACK_CHARINFO_PER_PAGE", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcAcceptEnter20240619, "HC_ACCEPT_ENTER_20240619", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcNotifyZoneServer, "HC_NOTIFY_ZONESVR", 28, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),
                new RoPacketDefinition(RoPacketIds.HcNotifyZoneServer2, "HC_NOTIFY_ZONESVR2", 156, RoPacketDirection.ServerToClient, RoPacketFamily.Hc),

                new RoPacketDefinition(0x0436, "CZ_ENTER2", 23, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x007d, "CZ_NOTIFY_ACTORINIT", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00b2, "CZ_RESTART", 3, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0292, "CZ_STANDING_RESURRECTION", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x035f, "CZ_REQUEST_MOVE", 5, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0360, "CZ_REQUEST_TIME", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0361, "CZ_CHANGE_DIRECTION", 5, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0437, "CZ_REQUEST_ACT", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0368, "CZ_REQUEST_NAME2", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x02d6, "CZ_EQUIPWIN_MICROSCOPE", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x02d8, "CZ_CONFIG", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0090, "CZ_CONTACTNPC", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00b9, "CZ_REQ_NEXT_SCRIPT", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0146, "CZ_CLOSE_DIALOG", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0ba8, "CZ_NPC_SELECT_MENU", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00c5, "CZ_ACK_SELECT_DEALTYPE", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0143, "CZ_INPUT_EDITDLG", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x01d5, "CZ_INPUT_EDITDLGSTR", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00c8, "CZ_PC_PURCHASE_ITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00c9, "CZ_PC_SELL_ITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqCloseStore, "CZ_REQ_CLOSESTORE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqBuyFromMc, "CZ_REQ_BUY_FROMMC", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzPcPurchaseItemListFromMc, "CZ_PC_PURCHASE_ITEMLIST_FROMMC", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqOpenStore2, "CZ_REQ_OPENSTORE2", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzPcPurchaseItemListFromMc2, "CZ_PC_PURCHASE_ITEMLIST_FROMMC2", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcTradeQuit, "CZ_NPC_TRADE_QUIT", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcMarketPurchase, "CZ_NPC_MARKET_PURCHASE", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcMarketClose, "CZ_NPC_MARKET_CLOSE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcBarterMarketPurchase, "CZ_NPC_BARTER_MARKET_PURCHASE", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcBarterMarketClose, "CZ_NPC_BARTER_MARKET_CLOSE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcExpandedBarterMarketPurchase, "CZ_NPC_EXPANDED_BARTER_MARKET_PURCHASE", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzNpcExpandedBarterMarketClose, "CZ_NPC_EXPANDED_BARTER_MARKET_CLOSE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0364, "CZ_MOVE_ITEM_FROM_BODY_TO_STORE2", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0365, "CZ_MOVE_ITEM_FROM_STORE_TO_BODY2", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0126, "CZ_MOVE_ITEM_FROM_BODY_TO_CART", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0127, "CZ_MOVE_ITEM_FROM_CART_TO_BODY", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0128, "CZ_MOVE_ITEM_FROM_STORE_TO_CART", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0129, "CZ_MOVE_ITEM_FROM_CART_TO_STORE", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x012a, "CZ_CARTOFF", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqItemIdentify, "CZ_REQ_ITEMIDENTIFY", 4, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqItemCompositionList, "CZ_REQ_ITEMCOMPOSITION_LIST", 4, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqItemComposition, "CZ_REQ_ITEMCOMPOSITION", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqMakingArrow, "CZ_REQ_MAKINGARROW", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqChangeCart, "CZ_REQ_CHANGECART", 4, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzSelectCart, "CZ_SELECTCART", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzSelectWarpPoint, "CZ_SELECT_WARPPOINT", 20, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
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
                new RoPacketDefinition(0x0193, "CZ_CLOSE_STORE", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00e4, "CZ_REQ_EXCHANGE_ITEM", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00e6, "CZ_ACK_EXCHANGE_ITEM", 3, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00e8, "CZ_ADD_EXCHANGE_ITEM", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00eb, "CZ_CONCLUDE_EXCHANGE_ITEM", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00ed, "CZ_CANCEL_EXCHANGE_ITEM", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00ef, "CZ_EXEC_EXCHANGE_ITEM", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0187, "CZ_PING", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00f3, "CZ_REQUEST_CHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0108, "CZ_PARTY_CHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x098d, "CZ_CLAN_CHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0438, "CZ_USE_SKILL2", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0366, "CZ_USE_SKILL_TOGROUND2", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0439, "CZ_USE_ITEM2", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0362, "CZ_ITEM_PICKUP2", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0363, "CZ_ITEM_THROW2", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0998, "CZ_REQ_WEAR_EQUIP_V5", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00ab, "CZ_REQ_TAKEOFF_EQUIP", 4, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0b21, "CZ_SHORTCUT_KEY_CHANGE2", 13, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x00bb, "CZ_STATUS_UP", 5, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0112, "CZ_UPGRADE_SKILLLEVEL", 4, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x02b6, "CZ_ACTIVE_QUEST", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x01e8, "CZ_MAKE_GROUP2", 28, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x02c4, "CZ_PARTY_JOIN_REQ", 26, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x02c7, "CZ_PARTY_JOIN_REQ_ACK", 7, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0100, "CZ_LEAVE_GROUP", 2, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0103, "CZ_REQ_EXPEL_GROUP_MEMBER", 30, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x07d7, "CZ_GROUPINFO_CHANGE_V2", 8, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x07da, "CZ_CHANGE_GROUP_MASTER", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0202, "CZ_ADD_FRIENDS", 26, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0203, "CZ_DELETE_FRIENDS", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(0x0208, "CZ_ACK_REQ_ADD_FRIENDS", 14, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
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
                new RoPacketDefinition(RoPacketIds.CzReqEmotion, "CZ_REQ_EMOTION", 3, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqEmotionExpansion, "CZ_REQ_EMOTION_EXPANSION", 6, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
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
                new RoPacketDefinition(RoPacketIds.ZcHomunculusProperty4, "ZC_PROPERTY_HOMUN_4", 85, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcHomunculusChangeState, "ZC_CHANGESTATE_MER", 12, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcHomunculusSkillInfoList, "ZC_HOSKILLINFO_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcHomunculusParChange, "ZC_HO_PAR_CHANGE", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMercenaryInfo, "ZC_MER_INIT", 80, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMercenarySkillInfoList, "ZC_MER_SKILLINFO_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
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
                new RoPacketDefinition(RoPacketIds.ZcGuildInfo4, "ZC_GUILD_INFO", 118, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMemberMgrInfo3, "ZC_MEMBERMGR_INFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
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
                new RoPacketDefinition(RoPacketIds.CzReqJoinGuild, "CZ_REQ_JOIN_GUILD", 14, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzJoinGuild, "CZ_JOIN_GUILD", 10, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.CzReqLeaveGuild, "CZ_REQ_LEAVE_GUILD", 54, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcBanList3, "ZC_BAN_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.CzCommandPet, "CZ_COMMAND_PET", 3, RoPacketDirection.ClientToServer, RoPacketFamily.Cz),
                new RoPacketDefinition(RoPacketIds.ZcPetProperty, "ZC_PROPERTY_PET", 37, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcFeedPet, "ZC_FEED_PET", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPetChangeState, "ZC_CHANGESTATE_PET", 11, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPetAct, "ZC_PET_ACT", 10, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
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

                new RoPacketDefinition(RoPacketIds.ZcRefuseEnter, "ZC_REFUSE_ENTER", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAcceptEnter, "ZC_ACCEPT_ENTER", 11, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAcceptEnter2, "ZC_ACCEPT_ENTER2", 13, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAcceptEnter3, "ZC_ACCEPT_ENTER3", 14, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAid, "ZC_AID", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcExtendBodyItemSize, "ZC_EXTEND_BODYITEM_SIZE", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyWeightPercentage, "ZC_NOTIFY_WEIGHT_PERCENTAGE", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcCoupleStatus, "ZC_COUPLESTATUS", 14, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyTime, "ZC_NOTIFY_TIME", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcAckMapMove, "ZC_NPCACK_MAPMOVE", 22, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyChat, "ZC_NOTIFY_CHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyPlayerChat, "ZC_NOTIFY_PLAYERCHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcBroadcast, "ZC_BROADCAST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcBroadcast2, "ZC_BROADCAST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcChat, "ZC_NPC_CHAT", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMsg, "ZC_MSG", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcFormatStringMsg, "ZC_FORMATSTRING_MSG", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMsgValue, "ZC_MSG_VALUE", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMsgSkill, "ZC_MSG_SKILL", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMsgColor, "ZC_MSG_COLOR", 8, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcFormatStringMsgColor, "ZC_FORMATSTRING_MSG_COLOR", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDebugMsg, "ZC_DEBUGMSG", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcShowScript, "ZC_SHOWSCRIPT", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAutoRunSkill, "ZC_AUTORUN_SKILL", 39, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSkillInfoUpdate, "ZC_SKILLINFO_UPDATE", 11, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAddSkill, "ZC_ADD_SKILL", 39, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSkillInfoUpdate2, "ZC_SKILLINFO_UPDATE2", 15, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAchievementUpdate, "ZC_ACH_UPDATE", 66, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(0x0a26, "ZC_REQ_ACH_REWARD_ACK", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcEquipSwitchList, "ZC_EQUIPSWITCH_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcItemPickupAck8, "ZC_ITEM_PICKUP_ACK8", 70, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcItemIdentifyList, "ZC_ITEMIDENTIFY_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckItemIdentify, "ZC_ACK_ITEMIDENTIFY", 5, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcMakingArrowList, "ZC_MAKINGARROW_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcSelectCart, "ZC_SELECTCART", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcWarpList, "ZC_WARPLIST", 68, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcWarpList2, "ZC_WARPLIST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNotifyMapInfo, "ZC_NOTIFY_MAPINFO", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcItemCompositionList, "ZC_ITEMCOMPOSITION_LIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckItemComposition, "ZC_ACK_ITEMCOMPOSITION", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcMarketOpen, "ZC_NPC_MARKET_OPEN", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcMarketPurchaseResult, "ZC_NPC_MARKET_PURCHASE_RESULT", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcMarketPurchaseResult2, "ZC_NPC_MARKET_PURCHASE_RESULT2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseItemList2, "ZC_PC_PURCHASE_ITEMLIST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcOpenStore, "ZC_OPENSTORE", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcStoreEntry, "ZC_STORE_ENTRY", 86, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDisappearEntry, "ZC_DISAPPEAR_ENTRY", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseItemListFromMc, "ZC_PC_PURCHASE_ITEMLIST_FROMMC", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseItemListFromMc2, "ZC_PC_PURCHASE_ITEMLIST_FROMMC2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseItemListFromMc3, "ZC_PC_PURCHASE_ITEMLIST_FROMMC3", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseResultFromMc, "ZC_PC_PURCHASE_RESULT_FROMMC", 7, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseMyItemList, "ZC_PC_PURCHASE_MYITEMLIST", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcPcPurchaseMyItemList2, "ZC_PC_PURCHASE_MYITEMLIST2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDeleteItemFromMcStore, "ZC_DELETEITEM_FROM_MCSTORE", 6, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcDeleteItemFromMcStore2, "ZC_DELETEITEM_FROM_MCSTORE2", 18, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckOpenStore2, "ZC_ACK_OPENSTORE2", 3, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcBarterMarketItemInfo, "ZC_NPC_BARTER_MARKET_ITEMINFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcExpandedBarterMarketItemInfo, "ZC_NPC_EXPANDED_BARTER_MARKET_ITEMINFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcNpcMarketOpen2, "ZC_NPC_MARKET_OPEN2", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcBankingCheck, "ZC_BANKING_CHECK", 12, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckBankingDeposit, "ZC_ACK_BANKING_DEPOSIT", 16, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckBankingWithdraw, "ZC_ACK_BANKING_WITHDRAW", 16, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckBankOpen, "ZC_ACK_OPEN_BANKING", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
                new RoPacketDefinition(RoPacketIds.ZcAckBankClose, "ZC_ACK_CLOSE_BANKING", 4, RoPacketDirection.ServerToClient, RoPacketFamily.Zc),
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
                new RoPacketDefinition(0x0b8d, "ZC_REPUTE_INFO", RoPacketDefinition.VariableLength, RoPacketDirection.ServerToClient, RoPacketFamily.Zc)
            };

            AddCustom20240619FramingDefinitions(definitions);
            return new RoPacketRegistry(definitions);
        }

        private static void AddCustom20240619FramingDefinitions(List<RoPacketDefinition> definitions) {
            var knownPacketIds = new HashSet<ushort>();
            for (int i = 0; i < definitions.Count; i++) {
                knownPacketIds.Add(definitions[i].PacketId);
            }

            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0069, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x006b, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x006c, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x006d, 157);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x006e, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0071, 28);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0073, 11);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0074, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0078, 55);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0079, 53);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x007a, 58);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x007b, 60);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x007c, 42);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x007f, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0080, 7);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0081, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0083, 2);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0084, 2);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0086, 16);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0087, 12);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0088, 10);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x008a, 29);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x008d, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x008e, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0091, 22);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0092, 28);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0095, 30);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0096, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0097, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0098, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x009a, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x009c, 9);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x009d, 19);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00a1, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00a3, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00a4, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00a5, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00a6, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00a8, 7);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00ac, 7);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00af, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00b0, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00b1, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00b3, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00b4, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00b5, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00b6, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00b7, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00bc, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00bd, 44);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00be, 5);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00c0, 7);
            // ZC_USER_COUNT (/who) and ZC_ACK_REMEMBER_WARPPOINT (/memo, Warp Portal): unframed they ended the session
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00c2, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x011e, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00c3, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00c4, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00c6, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00c7, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00ca, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00cb, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00e5, 26);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00e7, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00e9, 19);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00ea, 5);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00ec, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00ee, 2);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00f0, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00f2, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00f4, 31);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00f6, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00f8, 2);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00fa, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00fb, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00fd, 27);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x00fe, 30);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0100, 2);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0101, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0104, 79);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0105, 31);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0106, 10);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0107, 10);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0109, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x010e, 11);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x010f, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0110, 14);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0111, 39);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0114, 31);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0115, 35);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0117, 18);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0119, 13);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x011a, 15);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x011f, 16);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0120, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0121, 14);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0122, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0123, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0124, 21);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0125, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x012b, 2);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0133, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0135, 7);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0139, 16);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x013a, 4);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x013b, 4);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x013c, 4);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x013d, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x013e, 24);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x013f, 26);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0140, 22);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0141, 14);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0142, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0144, 23);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0147, 39);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0148, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x014e, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0150, 110);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0154, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0169, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x016a, 30);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x016c, 43);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x016f, 182);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0176, 106);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x017f, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x018b, 4);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0195, 102);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0196, 9);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0199, 4);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x019b, 10);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01b0, 11);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01b3, 67);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01b6, 114);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01b9, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01d0, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01e1, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01c3, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01c4, 32);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01c8, 15);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01c9, 97);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01d4, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01d5, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01d6, 4);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01d7, 15);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01de, 33);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01e9, 81);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01eb, 10);
            // ZC_BOSS_INFO (Convex Mirror) and the minimap ping: unframed they ended the session as unknown packets
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0293, 70);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09c1, 10);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01f0, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01f2, 20);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01f3, 10);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01f4, 32);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01f5, 9);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x01ff, 10);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0201, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0206, 35);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0207, 34);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0209, 36);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x020a, 10);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0229, 15);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0276, 51);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0283, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0291, 4);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0296, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02b4, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02b5, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02b7, 7);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02c1, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02c5, 30);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02c6, 30);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02c9, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02ca, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02cb, 65);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02cc, 4);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02cd, 71);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02ce, 10);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02cf, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02d1, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02d2, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02d9, 10);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02da, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02e1, 33);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02e9, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02ea, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02eb, 13);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02f7, 47);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x043d, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x043e, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x043f, 25);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0441, 4);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0446, 14);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x07d8, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x07db, 15);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x07e1, 15);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x07e2, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x07fa, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x07fb, 25);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x07fc, 10);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0800, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x080e, 14);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x080f, 20);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x082d, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x083a, 5);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0840, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x08b3, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x08b9, 12);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x08c7, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x08c8, 34);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x08d0, 9);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x08d1, 7);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x08d6, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x08fe, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x08ff, 24);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0977, 14);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0983, 29);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0984, 28);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x098e, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0991, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0992, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0993, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0994, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0995, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0996, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0999, 11);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x099a, 9);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x099b, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x099d, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x099f, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09a6, 12);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09a8, 16);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09aa, 16);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09ca, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09cb, 17);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09de, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09df, 7);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09e7, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09ed, 3);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09f2, 12);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09f4, 12);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09f6, 11);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09fa, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09fd, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09fe, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x09ff, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a09, 45);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a0a, 57);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a0b, 57);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a0d, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a0f, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a10, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a18, 14);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a23, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a27, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a30, 106);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a3b, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a43, 85);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a44, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a95, 4);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0a96, 51);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0aa8, 5);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0ab2, 7);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0abd, 10);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0ac4, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0ac5, 156);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0acb, 12);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0acc, 18);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0adc, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0add, 24);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0ade, 6);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0adf, 58);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0ae2, 7);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0ae4, 89);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0ae5, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0afe, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0aff, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b02, 26);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b08, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b09, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b0a, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b0b, 4);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b0c, 155);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b18, 4);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b1a, 29);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b1b, 2);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b1d, 2);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b20, 271);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, RoPacketIds.ZcEquipWinMicroscope, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b39, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b41, 70);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b42, 62);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b44, 58);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b45, 58);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b6f, 177);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b72, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b77, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b78, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0b79, RoPacketDefinition.VariableLength);
            // the typed chat lines' packets (docs/audits/chat-types-2026-10-04.md), lengths from the client's table
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x07f6, 14);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x02bb, 8);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x081f, RoPacketDefinition.VariableLength);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x097d, 288);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x097e, 12);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0af6, 88);
            AddCustom20240619FramingDefinition(definitions, knownPacketIds, 0x0215, 6);
        }

        private static void AddCustom20240619FramingDefinition(
            List<RoPacketDefinition> definitions,
            HashSet<ushort> knownPacketIds,
            ushort packetId,
            int length) {
            if (knownPacketIds.Contains(packetId)) {
                return;
            }

            string packetName;
            RoPacketFamily packetFamily;
            if (!RoPacketNameTable20200401.TryGet(packetId, out packetName, out packetFamily)) {
                packetName = $"CUSTOM_20240619_0x{packetId:x4}";
                packetFamily = RoPacketFamily.Unknown;
            }

            definitions.Add(new RoPacketDefinition(
                packetId,
                packetName,
                length,
                DirectionOfFamily(packetFamily),
                packetFamily));
            knownPacketIds.Add(packetId);
        }

        /// <summary>A framing-only packet the name table knows: its family gives the direction (ZC/HC/AC to the client).</summary>
        private static RoPacketDirection DirectionOfFamily(RoPacketFamily family) {
            switch (family) {
                case RoPacketFamily.Zc:
                case RoPacketFamily.Hc:
                case RoPacketFamily.Ac:
                    return RoPacketDirection.ServerToClient;
                case RoPacketFamily.Cz:
                case RoPacketFamily.Ch:
                case RoPacketFamily.Ca:
                    return RoPacketDirection.ClientToServer;
                default:
                    return RoPacketDirection.Unknown;
            }
        }
    }
}
