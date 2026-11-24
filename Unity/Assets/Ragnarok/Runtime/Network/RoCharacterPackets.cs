using System;
using System.Collections.Generic;
using System.Text;

namespace Ragnarok.Network {
    public sealed class RoCharacterInfo {
        public const int BlockSize20200401 = 155;
        public const int BlockSize20240619 = 175;

        internal RoCharacterInfo() {
        }

        public int CharacterId { get; internal set; }
        public long Experience { get; internal set; }
        public int Money { get; internal set; }
        public long JobExperience { get; internal set; }
        public int JobLevel { get; internal set; }
        public int BodyState { get; internal set; }
        public int HealthState { get; internal set; }
        public int EffectState { get; internal set; }
        public int Virtue { get; internal set; }
        public int Honor { get; internal set; }
        public short JobPoint { get; internal set; }
        public int Hp { get; internal set; }
        public int MaxHp { get; internal set; }
        public short Sp { get; internal set; }
        public short MaxSp { get; internal set; }
        public short Speed { get; internal set; }
        public short JobId { get; internal set; }
        public short Head { get; internal set; }
        public short Body { get; internal set; }
        public short Weapon { get; internal set; }
        public short BaseLevel { get; internal set; }
        public short SkillPoint { get; internal set; }
        public short Accessory { get; internal set; }
        public short Shield { get; internal set; }
        public short Accessory2 { get; internal set; }
        public short Accessory3 { get; internal set; }
        public short HeadPalette { get; internal set; }
        public short BodyPalette { get; internal set; }
        public string Name { get; internal set; }
        public byte Strength { get; internal set; }
        public byte Agility { get; internal set; }
        public byte Vitality { get; internal set; }
        public byte Intelligence { get; internal set; }
        public byte Dexterity { get; internal set; }
        public byte Luck { get; internal set; }
        public byte Slot { get; internal set; }
        public byte HairColor { get; internal set; }
        public short NameChangeFlag { get; internal set; }
        public string MapName { get; internal set; }
        public int DeleteRevisionDate { get; internal set; }
        public int RobePalette { get; internal set; }
        public int SlotChangeCount { get; internal set; }
        public int NameChangeCount { get; internal set; }
        public byte Sex { get; internal set; }
    }

    public readonly struct RoHcAcceptEnter {
        public RoHcAcceptEnter(int packetLength, byte totalSlots, byte premiumStart, byte premiumEnd, string extension, RoCharacterInfo[] characters) {
            PacketLength = packetLength;
            TotalSlots = totalSlots;
            PremiumStart = premiumStart;
            PremiumEnd = premiumEnd;
            Extension = extension ?? string.Empty;
            Characters = characters ?? Array.Empty<RoCharacterInfo>();
        }

        public int PacketLength { get; }
        public byte TotalSlots { get; }
        public byte PremiumStart { get; }
        public byte PremiumEnd { get; }
        public string Extension { get; }
        public RoCharacterInfo[] Characters { get; }
    }

    public readonly struct RoHcAcceptEnter2 {
        public RoHcAcceptEnter2(int packetLength, byte normalSlots, byte premiumSlots, byte billingSlots, byte producibleSlots, byte totalSlots, string extension) {
            PacketLength = packetLength;
            NormalSlots = normalSlots;
            PremiumSlots = premiumSlots;
            BillingSlots = billingSlots;
            ProducibleSlots = producibleSlots;
            TotalSlots = totalSlots;
            Extension = extension ?? string.Empty;
        }

        public int PacketLength { get; }
        public byte NormalSlots { get; }
        public byte PremiumSlots { get; }
        public byte BillingSlots { get; }
        public byte ProducibleSlots { get; }
        public byte TotalSlots { get; }
        public string Extension { get; }
    }

    public readonly struct RoHcNotifyCharList {
        public RoHcNotifyCharList(int totalSlots) {
            TotalSlots = totalSlots;
        }

        public int TotalSlots { get; }
    }

    public readonly struct RoAccessibleMapEntry {
        public RoAccessibleMapEntry(int status, string mapName) {
            Status = status;
            MapName = mapName ?? string.Empty;
        }

        public int Status { get; }
        public string MapName { get; }
        public bool IsAvailable => Status == 0;
    }

    public readonly struct RoHcNotifyAccessibleMapName {
        public RoHcNotifyAccessibleMapName(int packetLength, RoAccessibleMapEntry[] maps) {
            PacketLength = packetLength;
            Maps = maps ?? Array.Empty<RoAccessibleMapEntry>();
        }

        public int PacketLength { get; }
        public RoAccessibleMapEntry[] Maps { get; }

        public int FindFirstAvailableMapIndex() {
            for (int i = 0; i < Maps.Length; i++) {
                if (Maps[i].IsAvailable) {
                    return i;
                }
            }

            return -1;
        }
    }

    public readonly struct RoCharServerAccountAck {
        public RoCharServerAccountAck(int accountId) {
            AccountId = accountId;
        }

        public int AccountId { get; }
    }

    public readonly struct RoHcAckCharInfoPerPage {
        public RoHcAckCharInfoPerPage(int packetLength, RoCharacterInfo[] characters) {
            PacketLength = packetLength;
            Characters = characters ?? Array.Empty<RoCharacterInfo>();
        }

        public int PacketLength { get; }
        public RoCharacterInfo[] Characters { get; }
    }

    public readonly struct RoHcBlockCharacterEntry {
        public RoHcBlockCharacterEntry(int characterId, string unblockTime) {
            CharacterId = characterId;
            UnblockTime = unblockTime ?? string.Empty;
        }

        public int CharacterId { get; }
        public string UnblockTime { get; }
    }

    public readonly struct RoHcBlockCharacter {
        public RoHcBlockCharacter(int packetLength, RoHcBlockCharacterEntry[] entries) {
            PacketLength = packetLength;
            Entries = entries ?? Array.Empty<RoHcBlockCharacterEntry>();
        }

        public int PacketLength { get; }
        public RoHcBlockCharacterEntry[] Entries { get; }
    }

    public readonly struct RoHcSecondPasswordLogin {
        public RoHcSecondPasswordLogin(int seed, int accountId, ushort result) {
            Seed = seed;
            AccountId = accountId;
            Result = result;
        }

        public int Seed { get; }
        public int AccountId { get; }
        public ushort Result { get; }
        public bool IsPassedOrDisabled => Result == 0;
    }

    public readonly struct RoHcAcceptMakeChar {
        public RoHcAcceptMakeChar(RoCharacterInfo character) {
            Character = character ?? throw new ArgumentNullException(nameof(character));
        }

        public RoCharacterInfo Character { get; }
    }

    public readonly struct RoHcRefuseEnter {
        public RoHcRefuseEnter(byte errorCode) {
            ErrorCode = errorCode;
        }

        public byte ErrorCode { get; }
    }

    public readonly struct RoHcRefuseMakeChar {
        public RoHcRefuseMakeChar(byte errorCode) {
            ErrorCode = errorCode;
        }

        public byte ErrorCode { get; }
    }

    public readonly struct RoHcDeleteChar3Reserved {
        public RoHcDeleteChar3Reserved(int characterId, int result, int deleteDate) {
            CharacterId = characterId;
            Result = result;
            DeleteDate = deleteDate;
        }

        public int CharacterId { get; }
        public int Result { get; }
        public int DeleteDate { get; }
        public bool IsSuccess => Result == 1;
    }

    public readonly struct RoHcDeleteChar3 {
        public RoHcDeleteChar3(int characterId, int result) {
            CharacterId = characterId;
            Result = result;
        }

        public int CharacterId { get; }
        public int Result { get; }
        public bool IsSuccess => Result == 1;
    }

    /// <summary>The answer to CH_DELETE_CHAR2: HC_ACCEPT_DELETECHAR 0x6f {} or HC_REFUSE_DELETECHAR 0x70 {error u8}.</summary>
    public readonly struct RoHcDeleteChar {
        public RoHcDeleteChar(bool accepted, int errorCode) {
            Accepted = accepted;
            ErrorCode = errorCode;
        }

        public bool Accepted { get; }
        public int ErrorCode { get; }
        public bool IsSuccess => Accepted;
    }

    public readonly struct RoHcDeleteChar3Cancel {
        public RoHcDeleteChar3Cancel(int characterId, int result) {
            CharacterId = characterId;
            Result = result;
        }

        public int CharacterId { get; }
        public int Result { get; }
        public bool IsSuccess => Result == 1;
    }

    public readonly struct RoHcNotifyZoneServer2 {
        public RoHcNotifyZoneServer2(int characterId, string mapName, string address, ushort port, string domain) {
            CharacterId = characterId;
            MapName = mapName ?? string.Empty;
            Address = address ?? string.Empty;
            Port = port;
            Domain = domain ?? string.Empty;
        }

        public int CharacterId { get; }
        public string MapName { get; }
        public string Address { get; }
        public ushort Port { get; }
        public string Domain { get; }

        public override string ToString() {
            return $"{MapName} {Address}:{Port}";
        }
    }

    public static class RoCharacterPacketParser {
        private const int HcAcceptEnter2Length = 29;
        private const int HcNotifyCharListLength = 6;
        private const int HcNotifyZoneServerLength = 28;
        private const int HcNotifyZoneServer2Length = 156;
        private const int HcAccessibleMapEntryLength = 20;
        private const int MapNameLengthExt = 16;
        private const int HcBlockCharacterEntryLength = 24;
        private const int HcSecondPasswordLoginLength = 12;
        private const int HcRefuseEnterLength = 3;
        private const int HcAcceptMakeCharLength = 2 + RoCharacterInfo.BlockSize20200401;
        private const int HcAcceptMakeChar20201007Length = 2 + RoCharacterInfo.BlockSize20240619;
        private const int HcRefuseMakeCharLength = 3;
        private const int HcDeleteChar3ReservedLength = 14;
        private const int HcDeleteChar3Length = 10;
        private const int HcDeleteChar3CancelLength = 10;

        public static RoHcAcceptEnter ParseHcAcceptEnter(byte[] frame, Encoding encoding = null) {
            var reader = CreateFrameReader(frame, RoPacketIds.HcAcceptEnter);
            int packetLength = reader.ReadUInt16();
            if (packetLength != frame.Length) {
                throw new ArgumentException($"HC_ACCEPT_ENTER length header {packetLength} does not match frame length {frame.Length}.", nameof(frame));
            }

            byte totalSlots = reader.ReadByte();
            byte premiumStart = reader.ReadByte();
            byte premiumEnd = reader.ReadByte();
            string extension = reader.ReadFixedString(20, encoding);

            if (reader.Remaining % RoCharacterInfo.BlockSize20200401 != 0) {
                throw new ArgumentException("HC_ACCEPT_ENTER character bytes do not match the 20200401 CHARACTER_INFO layout.", nameof(frame));
            }

            var characters = new List<RoCharacterInfo>();
            while (!reader.EndOfPacket) {
                characters.Add(ReadCharacterInfo(reader, encoding));
            }

            return new RoHcAcceptEnter(packetLength, totalSlots, premiumStart, premiumEnd, extension, characters.ToArray());
        }

        public static RoHcAcceptEnter ParseHcAcceptEnter20240619Legacy(byte[] frame, Encoding encoding = null) {
            var reader = CreateFrameReader(frame, RoPacketIds.HcAcceptEnter);
            int packetLength = reader.ReadUInt16();
            if (packetLength != frame.Length) {
                throw new ArgumentException($"HC_ACCEPT_ENTER length header {packetLength} does not match frame length {frame.Length}.", nameof(frame));
            }

            byte totalSlots = reader.ReadByte();
            byte premiumStart = reader.ReadByte();
            byte premiumEnd = reader.ReadByte();
            reader.Skip(13);
            string extension = reader.ReadFixedString(7, encoding);

            if (reader.Remaining % RoCharacterInfo.BlockSize20240619 != 0) {
                throw new ArgumentException("HC_ACCEPT_ENTER character bytes do not match the 20240619 CHARACTER_INFO layout.", nameof(frame));
            }

            var characters = new List<RoCharacterInfo>();
            while (!reader.EndOfPacket) {
                characters.Add(ReadCharacterInfo20240619(reader, encoding));
            }

            return new RoHcAcceptEnter(packetLength, totalSlots, premiumStart, premiumEnd, extension, characters.ToArray());
        }

        public static RoHcAcceptEnter ParseHcAcceptEnter20240619(byte[] frame, Encoding encoding = null) {
            var reader = CreateFrameReader(frame, RoPacketIds.HcAcceptEnter20240619);
            int packetLength = reader.ReadUInt16();
            if (packetLength != frame.Length) {
                throw new ArgumentException($"HC_ACCEPT_ENTER_20240619 length header {packetLength} does not match frame length {frame.Length}.", nameof(frame));
            }

            if (reader.Remaining % RoCharacterInfo.BlockSize20240619 != 0) {
                throw new ArgumentException("HC_ACCEPT_ENTER_20240619 character bytes do not match the 20240619 CHARACTER_INFO layout.", nameof(frame));
            }

            var characters = new List<RoCharacterInfo>();
            while (!reader.EndOfPacket) {
                characters.Add(ReadCharacterInfo20240619(reader, encoding));
            }

            return new RoHcAcceptEnter(packetLength, 0, 0, 0, string.Empty, characters.ToArray());
        }

        public static RoHcAckCharInfoPerPage ParseHcAckCharInfoPerPage(byte[] frame, Encoding encoding = null) {
            var reader = CreateFrameReader(frame, RoPacketIds.HcAckCharInfoPerPage);
            int packetLength = reader.ReadUInt16();
            if (packetLength != frame.Length) {
                throw new ArgumentException($"HC_ACK_CHARINFO_PER_PAGE length header {packetLength} does not match frame length {frame.Length}.", nameof(frame));
            }

            if (reader.Remaining % RoCharacterInfo.BlockSize20200401 != 0) {
                throw new ArgumentException("HC_ACK_CHARINFO_PER_PAGE character bytes do not match the 20200401 CHARACTER_INFO layout.", nameof(frame));
            }

            var characters = new List<RoCharacterInfo>();
            while (!reader.EndOfPacket) {
                characters.Add(ReadCharacterInfo(reader, encoding));
            }

            return new RoHcAckCharInfoPerPage(packetLength, characters.ToArray());
        }

        public static RoHcAckCharInfoPerPage ParseHcAckCharInfoPerPage20240619(byte[] frame, Encoding encoding = null) {
            var reader = CreateFrameReader(frame, RoPacketIds.HcAckCharInfoPerPage);
            int packetLength = reader.ReadUInt16();
            if (packetLength != frame.Length) {
                throw new ArgumentException($"HC_ACK_CHARINFO_PER_PAGE length header {packetLength} does not match frame length {frame.Length}.", nameof(frame));
            }

            if (reader.Remaining % RoCharacterInfo.BlockSize20240619 != 0) {
                throw new ArgumentException("HC_ACK_CHARINFO_PER_PAGE character bytes do not match the 20240619 CHARACTER_INFO layout.", nameof(frame));
            }

            var characters = new List<RoCharacterInfo>();
            while (!reader.EndOfPacket) {
                characters.Add(ReadCharacterInfo20240619(reader, encoding));
            }

            return new RoHcAckCharInfoPerPage(packetLength, characters.ToArray());
        }

        public static RoHcAcceptEnter2 ParseHcAcceptEnter2(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.HcAcceptEnter2, HcAcceptEnter2Length);
            int packetLength = reader.ReadUInt16();
            if (packetLength != frame.Length) {
                throw new ArgumentException($"HC_ACCEPT_ENTER2 length header {packetLength} does not match frame length {frame.Length}.", nameof(frame));
            }

            byte normal = reader.ReadByte();
            byte premium = reader.ReadByte();
            byte billing = reader.ReadByte();
            byte producible = reader.ReadByte();
            byte total = reader.ReadByte();
            string extension = reader.ReadFixedString(20, encoding);

            return new RoHcAcceptEnter2(packetLength, normal, premium, billing, producible, total, extension);
        }

        public static RoHcNotifyCharList ParseHcNotifyCharList(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.HcNotifyCharList, HcNotifyCharListLength);
            return new RoHcNotifyCharList(reader.ReadInt32());
        }

        public static RoHcNotifyAccessibleMapName ParseHcNotifyAccessibleMapName(byte[] frame, Encoding encoding = null) {
            var reader = CreateFrameReader(frame, RoPacketIds.HcNotifyAccessibleMapName);
            int packetLength = reader.ReadUInt16();
            if (packetLength != frame.Length) {
                throw new ArgumentException($"HC_NOTIFY_ACCESSIBLE_MAPNAME length header {packetLength} does not match frame length {frame.Length}.", nameof(frame));
            }

            if (reader.Remaining % HcAccessibleMapEntryLength != 0) {
                throw new ArgumentException("HC_NOTIFY_ACCESSIBLE_MAPNAME bytes do not match the accessible map entry layout.", nameof(frame));
            }

            var maps = new List<RoAccessibleMapEntry>();
            while (!reader.EndOfPacket) {
                int status = reader.ReadInt32();
                string mapName = reader.ReadFixedString(MapNameLengthExt, encoding);
                maps.Add(new RoAccessibleMapEntry(status, mapName));
            }

            return new RoHcNotifyAccessibleMapName(packetLength, maps.ToArray());
        }

        public static RoHcBlockCharacter ParseHcBlockCharacter(byte[] frame, Encoding encoding = null) {
            var reader = CreateFrameReader(frame, RoPacketIds.HcBlockCharacter);
            int packetLength = reader.ReadUInt16();
            if (packetLength != frame.Length) {
                throw new ArgumentException($"HC_BLOCK_CHARACTER length header {packetLength} does not match frame length {frame.Length}.", nameof(frame));
            }

            if (reader.Remaining % HcBlockCharacterEntryLength != 0) {
                throw new ArgumentException("HC_BLOCK_CHARACTER entry bytes do not match the 20200401 layout.", nameof(frame));
            }

            var entries = new List<RoHcBlockCharacterEntry>();
            while (!reader.EndOfPacket) {
                int characterId = reader.ReadInt32();
                string unblockTime = reader.ReadFixedString(20, encoding);
                entries.Add(new RoHcBlockCharacterEntry(characterId, unblockTime));
            }

            return new RoHcBlockCharacter(packetLength, entries.ToArray());
        }

        public static RoHcSecondPasswordLogin ParseHcSecondPasswordLogin(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.HcSecondPasswordLogin, HcSecondPasswordLoginLength);
            int seed = reader.ReadInt32();
            int accountId = reader.ReadInt32();
            ushort result = reader.ReadUInt16();
            return new RoHcSecondPasswordLogin(seed, accountId, result);
        }

        public static RoHcAcceptMakeChar ParseHcAcceptMakeChar(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.HcAcceptMakeChar, HcAcceptMakeCharLength);
            return new RoHcAcceptMakeChar(ReadCharacterInfo(reader, encoding));
        }

        public static RoHcAcceptMakeChar ParseHcAcceptMakeChar20201007(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.HcAcceptMakeChar20201007, HcAcceptMakeChar20201007Length);
            return new RoHcAcceptMakeChar(ReadCharacterInfo20240619(reader, encoding));
        }

        public static RoHcAcceptMakeChar ParseHcAcceptMakeCharForProfile(byte[] frame, Encoding encoding = null) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }

            if (frame.Length < 2) {
                throw new ArgumentException("Packet frame must include the 2-byte packet id.", nameof(frame));
            }

            ushort packetId = (ushort) (frame[0] | (frame[1] << 8));
            switch (packetId) {
                case RoPacketIds.HcAcceptMakeChar:
                    return ParseHcAcceptMakeChar(frame, encoding);
                case RoPacketIds.HcAcceptMakeChar20201007:
                    return ParseHcAcceptMakeChar20201007(frame, encoding);
                default:
                    throw new ArgumentException($"Packet 0x{packetId:x4} is not a supported character creation accept packet.", nameof(frame));
            }
        }

        public static RoHcRefuseEnter ParseHcRefuseEnter(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.HcRefuseEnter, HcRefuseEnterLength);
            return new RoHcRefuseEnter(reader.ReadByte());
        }

        public static RoHcRefuseMakeChar ParseHcRefuseMakeChar(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.HcRefuseMakeChar, HcRefuseMakeCharLength);
            return new RoHcRefuseMakeChar(reader.ReadByte());
        }

        public static RoHcDeleteChar3Reserved ParseHcDeleteChar3Reserved(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.HcDeleteChar3Reserved, HcDeleteChar3ReservedLength);
            return new RoHcDeleteChar3Reserved(
                unchecked((int) reader.ReadUInt32()),
                reader.ReadInt32(),
                reader.ReadInt32());
        }

        public static RoHcDeleteChar3 ParseHcDeleteChar3(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.HcDeleteChar3, HcDeleteChar3Length);
            return new RoHcDeleteChar3(
                unchecked((int) reader.ReadUInt32()),
                reader.ReadInt32());
        }

        public static RoHcDeleteChar ParseHcAcceptDeleteChar(byte[] frame) {
            CreateFixedFrameReader(frame, RoPacketIds.HcAcceptDeleteChar, 2);
            return new RoHcDeleteChar(true, 0);
        }

        public static RoHcDeleteChar ParseHcRefuseDeleteChar(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.HcRefuseDeleteChar, 3);
            return new RoHcDeleteChar(false, reader.ReadByte());
        }

        public static RoHcDeleteChar3Cancel ParseHcDeleteChar3Cancel(byte[] frame) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.HcDeleteChar3Cancel, HcDeleteChar3CancelLength);
            return new RoHcDeleteChar3Cancel(
                unchecked((int) reader.ReadUInt32()),
                reader.ReadInt32());
        }

        public static RoCharServerAccountAck ParseCharServerAccountAck(byte[] frame) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }

            if (frame.Length != 4) {
                throw new ArgumentException($"Char-server account ack must be 4 bytes, got {frame.Length}.", nameof(frame));
            }

            var reader = new RoPacketReader(frame);
            return new RoCharServerAccountAck(reader.ReadInt32());
        }

        public static RoHcNotifyZoneServer2 ParseHcNotifyZoneServer2(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.HcNotifyZoneServer2, HcNotifyZoneServer2Length);
            int characterId = reader.ReadInt32();
            string mapName = reader.ReadFixedString(16, encoding);
            string address = FormatIpv4(reader.ReadBytes(4));
            ushort port = reader.ReadUInt16();
            string domain = reader.ReadFixedString(128, encoding);

            return new RoHcNotifyZoneServer2(characterId, mapName, address, port, domain);
        }

        public static RoHcNotifyZoneServer2 ParseHcNotifyZoneServer(byte[] frame, Encoding encoding = null) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }

            if (frame.Length < 2) {
                throw new ArgumentException("Packet frame must include the 2-byte packet id.", nameof(frame));
            }

            ushort packetId = (ushort) (frame[0] | (frame[1] << 8));
            switch (packetId) {
                case RoPacketIds.HcNotifyZoneServer:
                    return ParseHcNotifyZoneServerLegacy(frame, encoding);
                case RoPacketIds.HcNotifyZoneServer2:
                    return ParseHcNotifyZoneServer2(frame, encoding);
                default:
                    throw new ArgumentException($"Packet 0x{packetId:x4} is not a supported map-handoff packet.", nameof(frame));
            }
        }

        private static RoHcNotifyZoneServer2 ParseHcNotifyZoneServerLegacy(byte[] frame, Encoding encoding = null) {
            var reader = CreateFixedFrameReader(frame, RoPacketIds.HcNotifyZoneServer, HcNotifyZoneServerLength);
            int characterId = reader.ReadInt32();
            string mapName = reader.ReadFixedString(16, encoding);
            string address = FormatIpv4(reader.ReadBytes(4));
            ushort port = reader.ReadUInt16();
            return new RoHcNotifyZoneServer2(characterId, mapName, address, port, string.Empty);
        }

        private static RoCharacterInfo ReadCharacterInfo(RoPacketReader reader, Encoding encoding) {
            return new RoCharacterInfo {
                CharacterId = reader.ReadInt32(),
                Experience = reader.ReadInt64(),
                Money = reader.ReadInt32(),
                JobExperience = reader.ReadInt64(),
                JobLevel = reader.ReadInt32(),
                BodyState = reader.ReadInt32(),
                HealthState = reader.ReadInt32(),
                EffectState = reader.ReadInt32(),
                Virtue = reader.ReadInt32(),
                Honor = reader.ReadInt32(),
                JobPoint = reader.ReadInt16(),
                Hp = reader.ReadInt32(),
                MaxHp = reader.ReadInt32(),
                Sp = reader.ReadInt16(),
                MaxSp = reader.ReadInt16(),
                Speed = reader.ReadInt16(),
                JobId = reader.ReadInt16(),
                Head = reader.ReadInt16(),
                Body = reader.ReadInt16(),
                Weapon = reader.ReadInt16(),
                BaseLevel = reader.ReadInt16(),
                SkillPoint = reader.ReadInt16(),
                Accessory = reader.ReadInt16(),
                Shield = reader.ReadInt16(),
                Accessory2 = reader.ReadInt16(),
                Accessory3 = reader.ReadInt16(),
                HeadPalette = reader.ReadInt16(),
                BodyPalette = reader.ReadInt16(),
                Name = reader.ReadFixedString(24, encoding),
                Strength = reader.ReadByte(),
                Agility = reader.ReadByte(),
                Vitality = reader.ReadByte(),
                Intelligence = reader.ReadByte(),
                Dexterity = reader.ReadByte(),
                Luck = reader.ReadByte(),
                Slot = reader.ReadByte(),
                HairColor = reader.ReadByte(),
                NameChangeFlag = reader.ReadInt16(),
                MapName = reader.ReadFixedString(16, encoding),
                DeleteRevisionDate = reader.ReadInt32(),
                RobePalette = reader.ReadInt32(),
                SlotChangeCount = reader.ReadInt32(),
                NameChangeCount = reader.ReadInt32(),
                Sex = reader.ReadByte(),
            };
        }

        private static RoCharacterInfo ReadCharacterInfo20240619(RoPacketReader reader, Encoding encoding) {
            int blockEnd = reader.Position + RoCharacterInfo.BlockSize20240619;
            RoCharacterInfo character = new RoCharacterInfo {
                CharacterId = unchecked((int) reader.ReadUInt32()),
                Experience = ClampToInt64(reader.ReadUInt64()),
                Money = reader.ReadInt32(),
                JobExperience = ClampToInt64(reader.ReadUInt64()),
                JobLevel = reader.ReadInt32(),
                BodyState = reader.ReadInt32(),
                HealthState = reader.ReadInt32(),
                EffectState = reader.ReadInt32(),
                Virtue = reader.ReadInt32(),
                Honor = reader.ReadInt32(),
                JobPoint = reader.ReadInt16(),
                Hp = ClampToInt32(reader.ReadUInt64()),
                MaxHp = ClampToInt32(reader.ReadUInt64()),
                Sp = ClampToInt16(reader.ReadUInt64()),
                MaxSp = ClampToInt16(reader.ReadUInt64()),
                Speed = reader.ReadInt16(),
                JobId = reader.ReadInt16(),
                Head = reader.ReadInt16(),
                Body = reader.ReadInt16(),
                Weapon = reader.ReadInt16(),
                BaseLevel = reader.ReadInt16(),
                SkillPoint = reader.ReadInt16(),
                Accessory = reader.ReadInt16(),
                Shield = reader.ReadInt16(),
                Accessory2 = reader.ReadInt16(),
                Accessory3 = reader.ReadInt16(),
                HeadPalette = reader.ReadInt16(),
                BodyPalette = reader.ReadInt16(),
                Name = reader.ReadFixedString(24, encoding),
                Strength = reader.ReadByte(),
                Agility = reader.ReadByte(),
                Vitality = reader.ReadByte(),
                Intelligence = reader.ReadByte(),
                Dexterity = reader.ReadByte(),
                Luck = reader.ReadByte(),
                Slot = reader.ReadByte(),
                HairColor = reader.ReadByte()
            };

            if (blockEnd - reader.Position >= 35) {
                character.NameChangeFlag = reader.ReadInt16();
            }

            if (blockEnd - reader.Position >= 16) {
                character.MapName = reader.ReadFixedString(16, encoding);
            }

            if (blockEnd - reader.Position >= 4) {
                character.DeleteRevisionDate = reader.ReadInt32();
            }

            if (blockEnd - reader.Position >= 4) {
                character.RobePalette = reader.ReadInt32();
            }

            if (blockEnd - reader.Position >= 4) {
                character.SlotChangeCount = reader.ReadInt32();
            }

            if (blockEnd - reader.Position >= 4) {
                character.NameChangeCount = reader.ReadInt32();
            }

            if (blockEnd - reader.Position >= 1) {
                character.Sex = reader.ReadByte();
            }

            if (reader.Position < blockEnd) {
                reader.Skip(blockEnd - reader.Position);
            }

            return character;
        }

        private static long ClampToInt64(ulong value) {
            return value > long.MaxValue ? long.MaxValue : (long) value;
        }

        private static int ClampToInt32(ulong value) {
            return value > int.MaxValue ? int.MaxValue : (int) value;
        }

        private static short ClampToInt16(ulong value) {
            return value > (ulong) short.MaxValue ? short.MaxValue : (short) value;
        }

        private static RoPacketReader CreateFixedFrameReader(byte[] frame, ushort expectedPacketId, int expectedLength) {
            var reader = CreateFrameReader(frame, expectedPacketId);
            if (frame.Length != expectedLength) {
                throw new ArgumentException($"Packet 0x{expectedPacketId:x4} must be {expectedLength} bytes, got {frame.Length}.", nameof(frame));
            }

            return reader;
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
