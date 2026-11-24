using System;
using System.Collections.Generic;
using System.Text;

namespace Ragnarok.Network {
    public readonly struct RoLoginCharServerInfo {
        public RoLoginCharServerInfo(string address, ushort port, string name, ushort userCount, ushort state, ushort property) {
            Address = address;
            Port = port;
            Name = name;
            UserCount = userCount;
            State = state;
            Property = property;
        }

        public string Address { get; }
        public ushort Port { get; }
        public string Name { get; }
        public ushort UserCount { get; }
        public ushort State { get; }
        public ushort Property { get; }

        public override string ToString() {
            return $"{Name} {Address}:{Port}";
        }
    }

    public readonly struct RoAcAcceptLogin3 {
        public RoAcAcceptLogin3(
            int loginId1,
            int accountId,
            int loginId2,
            uint lastIp,
            string lastLogin,
            byte sex,
            string webAuthToken,
            RoLoginCharServerInfo[] servers) {
            LoginId1 = loginId1;
            AccountId = accountId;
            LoginId2 = loginId2;
            LastIp = lastIp;
            LastLogin = lastLogin;
            Sex = sex;
            WebAuthToken = webAuthToken;
            Servers = servers ?? Array.Empty<RoLoginCharServerInfo>();
        }

        public int LoginId1 { get; }
        public int AccountId { get; }
        public int LoginId2 { get; }
        public uint LastIp { get; }
        public string LastLogin { get; }
        public byte Sex { get; }
        public string WebAuthToken { get; }
        public RoLoginCharServerInfo[] Servers { get; }
    }

    public readonly struct RoAcRefuseLogin {
        public RoAcRefuseLogin(int errorCode, string blockDate) {
            ErrorCode = errorCode;
            BlockDate = blockDate;
        }

        public int ErrorCode { get; }
        public string BlockDate { get; }
    }

    public static class RoLoginPacketParser {
        private const int AcAcceptLogin3BaseLength = 64;
        private const int AcAcceptLogin3CharServerBlockLength = 160;

        public static RoAcAcceptLogin3 ParseAcAcceptLogin3(byte[] frame, Encoding encoding = null) {
            var reader = CreateFrameReader(frame, RoPacketIds.AcAcceptLogin3);
            int packetLength = reader.ReadUInt16();
            if (packetLength != frame.Length) {
                throw new ArgumentException($"AC_ACCEPT_LOGIN3 length header {packetLength} does not match frame length {frame.Length}.", nameof(frame));
            }

            int loginId1 = reader.ReadInt32();
            int accountId = reader.ReadInt32();
            int loginId2 = reader.ReadInt32();
            uint lastIp = reader.ReadUInt32();
            string lastLogin = reader.ReadFixedString(26, encoding);
            byte sex = reader.ReadByte();
            string token = reader.ReadFixedString(17, encoding);

            if (reader.Remaining % AcAcceptLogin3CharServerBlockLength != 0) {
                throw new ArgumentException("AC_ACCEPT_LOGIN3 char-server block bytes do not match the 20200401 packet layout.", nameof(frame));
            }

            var servers = new List<RoLoginCharServerInfo>();
            while (!reader.EndOfPacket) {
                byte[] addressBytes = reader.ReadBytes(4);
                ushort port = reader.ReadUInt16();
                string name = reader.ReadFixedString(20, encoding);
                ushort userCount = reader.ReadUInt16();
                ushort state = reader.ReadUInt16();
                ushort property = reader.ReadUInt16();
                reader.Skip(128);

                servers.Add(new RoLoginCharServerInfo(
                    FormatIpv4(addressBytes),
                    port,
                    name,
                    userCount,
                    state,
                    property));
            }

            return new RoAcAcceptLogin3(loginId1, accountId, loginId2, lastIp, lastLogin, sex, token, servers.ToArray());
        }

        public static RoAcRefuseLogin ParseAcRefuseLoginR2(byte[] frame, Encoding encoding = null) {
            var reader = CreateFrameReader(frame, RoPacketIds.AcRefuseLoginR2);
            int errorCode = reader.ReadInt32();
            string blockDate = reader.ReadFixedString(20, encoding);
            return new RoAcRefuseLogin(errorCode, blockDate);
        }

        public static RoAcRefuseLogin ParseAcRefuseLogin20240619(byte[] frame, Encoding encoding = null) {
            var reader = CreateFrameReader(frame, RoPacketIds.AcRefuseLogin20240619);
            int errorCode = reader.ReadInt32();
            string blockDate = reader.ReadFixedString(20, encoding);
            return new RoAcRefuseLogin(errorCode, blockDate);
        }

        public static RoAcRefuseLogin ParseAcRefuseLogin(byte[] frame, Encoding encoding = null) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }

            if (frame.Length < 2) {
                throw new ArgumentException("Packet frame must include the 2-byte packet id.", nameof(frame));
            }

            ushort packetId = (ushort) (frame[0] | (frame[1] << 8));
            switch (packetId) {
                case RoPacketIds.AcRefuseLoginR2:
                    return ParseAcRefuseLoginR2(frame, encoding);
                case RoPacketIds.AcRefuseLogin20240619:
                    return ParseAcRefuseLogin20240619(frame, encoding);
                default:
                    throw new ArgumentException($"Packet 0x{packetId:x4} is not a supported login-refuse packet.", nameof(frame));
            }
        }

        private static RoPacketReader CreateFrameReader(byte[] frame, ushort expectedPacketId) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }

            if (frame.Length < 2) {
                throw new ArgumentException("Packet frame must include the 2-byte packet id.", nameof(frame));
            }

            var reader = new RoPacketReader(frame);
            ushort packetId = reader.ReadUInt16();
            if (packetId != expectedPacketId) {
                throw new ArgumentException($"Expected packet 0x{expectedPacketId:x4}, got 0x{packetId:x4}.", nameof(frame));
            }

            return reader;
        }

        private static string FormatIpv4(byte[] addressBytes) {
            if (addressBytes == null || addressBytes.Length != 4) {
                throw new ArgumentException("IPv4 address must be exactly 4 bytes.", nameof(addressBytes));
            }

            return $"{addressBytes[0]}.{addressBytes[1]}.{addressBytes[2]}.{addressBytes[3]}";
        }
    }
}
