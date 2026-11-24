using System;

namespace Ragnarok.Network {
    public readonly struct RoServerEndpoint {
        public RoServerEndpoint(string host, int port) {
            Host = host;
            Port = port;
        }

        public string Host { get; }
        public int Port { get; }

        public override string ToString() {
            return $"{Host}:{Port}";
        }

        public static RoServerEndpoint PreferProfileHostForLoopback(RoServerEndpoint advertisedEndpoint, RoServerEndpoint profileEndpoint) {
            if (string.IsNullOrWhiteSpace(profileEndpoint.Host)) {
                return advertisedEndpoint;
            }

            if (IsLoopbackHost(advertisedEndpoint.Host) ||
                IsPrivateNetworkHost(advertisedEndpoint.Host)) {
                return new RoServerEndpoint(profileEndpoint.Host, advertisedEndpoint.Port);
            }

            return advertisedEndpoint;
        }

        private static bool IsLoopbackHost(string host) {
            if (string.IsNullOrWhiteSpace(host)) {
                return false;
            }

            return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || host.Equals("::1", StringComparison.Ordinal)
                || host.StartsWith("127.", StringComparison.Ordinal);
        }

        private static bool IsPrivateNetworkHost(string host) {
            if (string.IsNullOrWhiteSpace(host)) {
                return false;
            }

            string value = host.Trim();
            if (value.StartsWith("10.", StringComparison.Ordinal) ||
                value.StartsWith("192.168.", StringComparison.Ordinal)) {
                return true;
            }

            if (!value.StartsWith("172.", StringComparison.Ordinal)) {
                return false;
            }

            string[] parts = value.Split('.');
            return parts.Length == 4 &&
                int.TryParse(parts[1], out int secondOctet) &&
                secondOctet >= 16 &&
                secondOctet <= 31;
        }
    }

    public readonly struct RoServerProfile {
        public RoServerProfile(
            string name,
            int packetVersion,
            int clientVersion,
            int languageType,
            bool renewal,
            bool packetObfuscation,
            RoServerEndpoint login,
            RoServerEndpoint character,
            RoServerEndpoint map,
            string webSocketProxy = null,
            int legacyTextCodePage = 949,
            RoPacketCryptConfig packetCryptConfig = default) {
            Name = name;
            PacketVersion = packetVersion;
            ClientVersion = clientVersion;
            LanguageType = languageType;
            Renewal = renewal;
            PacketObfuscation = packetObfuscation;
            Login = login;
            Character = character;
            Map = map;
            WebSocketProxy = webSocketProxy;
            LegacyTextCodePage = legacyTextCodePage;
            PacketCryptConfig = packetCryptConfig;
        }

        public string Name { get; }
        public int PacketVersion { get; }
        public int ClientVersion { get; }
        public int LanguageType { get; }
        public bool Renewal { get; }
        public bool PacketObfuscation { get; }
        public RoServerEndpoint Login { get; }
        public RoServerEndpoint Character { get; }
        public RoServerEndpoint Map { get; }
        public string WebSocketProxy { get; }
        public int LegacyTextCodePage { get; }
        public RoPacketCryptConfig PacketCryptConfig { get; }

        public static RoServerProfile LocalRathena20200401() {
            var local = new RoServerEndpoint("localhost", 0);
            return new RoServerProfile(
                "Local rAthena 20200401",
                packetVersion: 20200401,
                clientVersion: 55,
                languageType: 12,
                renewal: true,
                packetObfuscation: false,
                login: new RoServerEndpoint(local.Host, 6900),
                character: new RoServerEndpoint(local.Host, 6121),
                map: new RoServerEndpoint(local.Host, 5121),
                webSocketProxy: "ws://localhost:5999");
        }
    }
}
