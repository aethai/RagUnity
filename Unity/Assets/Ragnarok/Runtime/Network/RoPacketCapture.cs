using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Ragnarok.Network {
    public sealed class RoPacketCaptureEntry {
        public RoPacketCaptureEntry(
            long sequence,
            DateTimeOffset timestamp,
            RoPacketDirection direction,
            ushort packetId,
            string packetName,
            byte[] bytes,
            RoPacketFrameStatus status) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }

            Sequence = sequence;
            Timestamp = timestamp;
            Direction = direction;
            PacketId = packetId;
            PacketName = packetName ?? string.Empty;
            Bytes = new byte[bytes.Length];
            Array.Copy(bytes, Bytes, bytes.Length);
            Status = status;
        }

        public long Sequence { get; }
        public DateTimeOffset Timestamp { get; }
        public RoPacketDirection Direction { get; }
        public ushort PacketId { get; }
        public string PacketName { get; }
        public byte[] Bytes { get; }
        public int Length => Bytes.Length;
        public RoPacketFrameStatus Status { get; }
        public bool IsPacketFrame => PacketId != 0;

        public byte[] CloneBytes() {
            var copy = new byte[Bytes.Length];
            Array.Copy(Bytes, copy, Bytes.Length);
            return copy;
        }

        public bool TryCreateFrame(RoPacketRegistry registry, out RoPacketFrame frame, out RoPacketFrameMeasurement measurement) {
            if (registry == null) {
                throw new ArgumentNullException(nameof(registry));
            }

            if (!IsPacketFrame || Bytes.Length < 2) {
                frame = default;
                measurement = new RoPacketFrameMeasurement(Status, PacketId, Bytes.Length, default);
                return false;
            }

            measurement = registry.MeasureFrame(Bytes, 0, Bytes.Length);
            if (!measurement.IsComplete) {
                frame = default;
                return false;
            }

            frame = new RoPacketFrame(measurement.PacketId, CloneBytes(), measurement.Definition);
            return true;
        }
    }

    public sealed class RoPacketCaptureDocument {
        public RoPacketCaptureDocument(
            string profileName,
            int packetVersion,
            DateTimeOffset createdAt,
            IEnumerable<RoPacketCaptureEntry> entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            ProfileName = profileName ?? string.Empty;
            PacketVersion = packetVersion;
            CreatedAt = createdAt;

            var list = new List<RoPacketCaptureEntry>();
            foreach (RoPacketCaptureEntry entry in entries) {
                if (entry == null) {
                    continue;
                }

                list.Add(entry);
            }

            Entries = list.ToArray();
        }

        public string ProfileName { get; }
        public int PacketVersion { get; }
        public DateTimeOffset CreatedAt { get; }
        public RoPacketCaptureEntry[] Entries { get; }
    }

    public interface IRoPacketCaptureSink {
        void Write(
            DateTimeOffset timestamp,
            RoPacketDirection direction,
            ushort packetId,
            string packetName,
            byte[] bytes,
            RoPacketFrameStatus status);
    }

    public sealed class RoPacketCaptureBuffer : IRoPacketCaptureSink {
        private readonly Queue<RoPacketCaptureEntry> entries;
        private long nextSequence;

        public RoPacketCaptureBuffer(int capacity = 4096) {
            if (capacity <= 0) {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            Capacity = capacity;
            entries = new Queue<RoPacketCaptureEntry>(capacity);
        }

        public int Capacity { get; }
        public int Count => entries.Count;

        public RoPacketCaptureEntry[] Snapshot() {
            return entries.ToArray();
        }

        public RoPacketCaptureDocument ToDocument(RoServerProfile profile) {
            return ToDocument(profile.Name, profile.PacketVersion);
        }

        public RoPacketCaptureDocument ToDocument(string profileName, int packetVersion) {
            return new RoPacketCaptureDocument(profileName, packetVersion, DateTimeOffset.UtcNow, Snapshot());
        }

        public void Write(
            DateTimeOffset timestamp,
            RoPacketDirection direction,
            ushort packetId,
            string packetName,
            byte[] bytes,
            RoPacketFrameStatus status) {
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }

            while (entries.Count >= Capacity) {
                entries.Dequeue();
            }

            entries.Enqueue(new RoPacketCaptureEntry(nextSequence++, timestamp, direction, packetId, packetName, bytes, status));
        }

        public void Clear() {
            entries.Clear();
            nextSequence = 0;
        }
    }

    public static class RoPacketCaptureProfileResolver {
        public static IRoPacketProfile ResolvePacketProfile(RoPacketCaptureDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            return ResolvePacketProfile(document.ProfileName, document.PacketVersion);
        }

        public static IRoPacketProfile ResolvePacketProfile(RoPacketReplay replay) {
            if (replay == null) {
                throw new ArgumentNullException(nameof(replay));
            }

            return ResolvePacketProfile(replay.ProfileName, replay.PacketVersion);
        }

        public static IRoPacketProfile ResolvePacketProfile(string profileName, int packetVersion) {
            ValidateProfileMetadata(profileName, packetVersion);
            switch (packetVersion) {
                case 20200401:
                    return RoPacketProfiles.LocalRathena20200401();
                case 20240619:
                    return RoPacketProfiles.Rathena20240619Seed();
                default:
                    throw new NotSupportedException(
                        $"No packet capture replay profile is registered for packetver {packetVersion}.");
            }
        }

        public static RoPacketRegistry CreateRegistry(RoPacketCaptureDocument document) {
            return ResolvePacketProfile(document).CreateRegistry();
        }

        public static RoPacketRegistry CreateRegistry(RoPacketReplay replay) {
            return ResolvePacketProfile(replay).CreateRegistry();
        }

        public static void ValidateDocumentMatchesProfile(RoPacketCaptureDocument document, IRoPacketProfile profile) {
            if (profile == null) {
                throw new ArgumentNullException(nameof(profile));
            }

            ValidateDocumentMatchesProfile(document, profile.Name, profile.PacketVersion);
        }

        public static void ValidateDocumentMatchesProfile(RoPacketCaptureDocument document, RoServerProfile profile) {
            ValidateDocumentMatchesProfile(document, profile.Name, profile.PacketVersion);
        }

        private static void ValidateDocumentMatchesProfile(
            RoPacketCaptureDocument document,
            string profileName,
            int packetVersion) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            ValidateProfileMetadata(document.ProfileName, document.PacketVersion);
            if (document.PacketVersion != packetVersion) {
                throw new InvalidOperationException(
                    $"Packet capture packetver {document.PacketVersion} does not match replay profile '{profileName}' packetver {packetVersion}.");
            }
        }

        private static void ValidateProfileMetadata(string profileName, int packetVersion) {
            if (packetVersion <= 0) {
                throw new InvalidOperationException(
                    "Packet capture document does not declare packetver. Pass an explicit packet registry for legacy captures.");
            }

            if (string.IsNullOrWhiteSpace(profileName)) {
                return;
            }

            if (profileName.IndexOf("20200401", StringComparison.OrdinalIgnoreCase) >= 0 &&
                packetVersion != 20200401) {
                throw new InvalidOperationException(
                    $"Packet capture profile '{profileName}' does not match packetver {packetVersion}.");
            }

            if (profileName.IndexOf("20240619", StringComparison.OrdinalIgnoreCase) >= 0 &&
                packetVersion != 20240619) {
                throw new InvalidOperationException(
                    $"Packet capture profile '{profileName}' does not match packetver {packetVersion}.");
            }
        }
    }

    public sealed class RoPacketReplay {
        private readonly RoPacketCaptureEntry[] entries;
        private int cursor;

        public RoPacketReplay(RoPacketCaptureDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            ProfileName = document.ProfileName;
            PacketVersion = document.PacketVersion;
            entries = CopyEntries(document.Entries);
        }

        public RoPacketReplay(IEnumerable<RoPacketCaptureEntry> entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            ProfileName = string.Empty;
            PacketVersion = 0;
            this.entries = CopyEntries(entries);
        }

        public string ProfileName { get; }
        public int PacketVersion { get; }

        private static RoPacketCaptureEntry[] CopyEntries(IEnumerable<RoPacketCaptureEntry> entries) {
            var list = new List<RoPacketCaptureEntry>();
            foreach (RoPacketCaptureEntry entry in entries) {
                if (entry != null) {
                    list.Add(entry);
                }
            }

            return list.ToArray();
        }

        public int Count => entries.Length;
        public int Cursor => cursor;
        public bool IsComplete => cursor >= entries.Length;

        public void Reset() {
            cursor = 0;
        }

        public bool TryRead(out RoPacketCaptureEntry entry) {
            if (cursor >= entries.Length) {
                entry = null;
                return false;
            }

            entry = entries[cursor++];
            return true;
        }

        public bool TryReadFrame(
            RoPacketRegistry registry,
            out RoPacketFrame frame,
            out RoPacketCaptureEntry entry,
            out RoPacketFrameMeasurement measurement) {
            if (registry == null) {
                throw new ArgumentNullException(nameof(registry));
            }

            while (TryRead(out entry)) {
                if (entry.TryCreateFrame(registry, out frame, out measurement)) {
                    return true;
                }
            }

            frame = default;
            entry = null;
            measurement = default;
            return false;
        }
    }

    public enum RoPacketCaptureDecodeStatus {
        Raw = 0,
        Framed = 1,
        Decoded = 2,
        UnknownPacket = 3,
        InvalidLength = 4,
        DecodeFailed = 5
    }

    public sealed class RoPacketCaptureReportEntry {
        public RoPacketCaptureReportEntry(
            long sequence,
            RoPacketDirection direction,
            ushort packetId,
            string packetName,
            int length,
            RoPacketFrameStatus captureStatus,
            RoPacketFrameStatus frameStatus,
            RoPacketCaptureDecodeStatus decodeStatus,
            string summary) {
            Sequence = sequence;
            Direction = direction;
            PacketId = packetId;
            PacketName = packetName ?? string.Empty;
            Length = length;
            CaptureStatus = captureStatus;
            FrameStatus = frameStatus;
            DecodeStatus = decodeStatus;
            Summary = summary ?? string.Empty;
        }

        public long Sequence { get; }
        public RoPacketDirection Direction { get; }
        public ushort PacketId { get; }
        public string PacketName { get; }
        public int Length { get; }
        public RoPacketFrameStatus CaptureStatus { get; }
        public RoPacketFrameStatus FrameStatus { get; }
        public RoPacketCaptureDecodeStatus DecodeStatus { get; }
        public string Summary { get; }
    }

    public sealed class RoPacketCaptureReport {
        public RoPacketCaptureReport(IEnumerable<RoPacketCaptureReportEntry> entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            var list = new List<RoPacketCaptureReportEntry>();
            foreach (RoPacketCaptureReportEntry entry in entries) {
                if (entry == null) {
                    continue;
                }

                list.Add(entry);
                switch (entry.DecodeStatus) {
                    case RoPacketCaptureDecodeStatus.Decoded:
                        DecodedCount++;
                        break;
                    case RoPacketCaptureDecodeStatus.UnknownPacket:
                        UnknownPacketCount++;
                        break;
                    case RoPacketCaptureDecodeStatus.InvalidLength:
                        InvalidLengthCount++;
                        break;
                    case RoPacketCaptureDecodeStatus.DecodeFailed:
                        DecodeFailedCount++;
                        break;
                }
            }

            Entries = list.ToArray();
        }

        public RoPacketCaptureReportEntry[] Entries { get; }
        public int Count => Entries.Length;
        public int DecodedCount { get; }
        public int UnknownPacketCount { get; }
        public int InvalidLengthCount { get; }
        public int DecodeFailedCount { get; }
        public bool HasProblems => UnknownPacketCount > 0 || InvalidLengthCount > 0 || DecodeFailedCount > 0;

        public string ToText() {
            var builder = new StringBuilder();
            builder.Append("seq dir id name len frame decode summary");
            builder.AppendLine();
            foreach (RoPacketCaptureReportEntry entry in Entries) {
                builder.Append(entry.Sequence.ToString(CultureInfo.InvariantCulture));
                builder.Append(' ');
                builder.Append(entry.Direction);
                builder.Append(' ');
                builder.Append("0x");
                builder.Append(entry.PacketId.ToString("x4", CultureInfo.InvariantCulture));
                builder.Append(' ');
                builder.Append(string.IsNullOrEmpty(entry.PacketName) ? "-" : entry.PacketName);
                builder.Append(' ');
                builder.Append(entry.Length.ToString(CultureInfo.InvariantCulture));
                builder.Append(' ');
                builder.Append(entry.FrameStatus);
                builder.Append(' ');
                builder.Append(entry.DecodeStatus);
                builder.Append(' ');
                builder.Append(entry.Summary);
                builder.AppendLine();
            }

            return builder.ToString();
        }
    }

    public static class RoPacketCaptureReporter {
        public static RoPacketCaptureReport Build(
            RoPacketCaptureDocument document,
            RoPacketRegistry registry = null,
            Encoding textEncoding = null) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            registry = registry ?? RoPacketCaptureProfileResolver.CreateRegistry(document);
            var entries = new List<RoPacketCaptureReportEntry>();
            foreach (RoPacketCaptureEntry entry in document.Entries) {
                entries.Add(BuildEntry(entry, registry, textEncoding));
            }

            return new RoPacketCaptureReport(entries);
        }

        private static RoPacketCaptureReportEntry BuildEntry(
            RoPacketCaptureEntry entry,
            RoPacketRegistry registry,
            Encoding textEncoding) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!entry.IsPacketFrame || entry.Bytes.Length < 2) {
                return new RoPacketCaptureReportEntry(
                    entry.Sequence,
                    entry.Direction,
                    entry.PacketId,
                    entry.PacketName,
                    entry.Length,
                    entry.Status,
                    entry.Status,
                    RoPacketCaptureDecodeStatus.Raw,
                    string.IsNullOrEmpty(entry.PacketName) ? "raw bytes" : entry.PacketName);
            }

            if (!entry.TryCreateFrame(registry, out RoPacketFrame frame, out RoPacketFrameMeasurement measurement)) {
                RoPacketCaptureDecodeStatus decodeStatus = measurement.Status == RoPacketFrameStatus.UnknownPacket
                    ? RoPacketCaptureDecodeStatus.UnknownPacket
                    : RoPacketCaptureDecodeStatus.InvalidLength;
                return new RoPacketCaptureReportEntry(
                    entry.Sequence,
                    entry.Direction,
                    measurement.PacketId,
                    entry.PacketName,
                    entry.Length,
                    entry.Status,
                    measurement.Status,
                    decodeStatus,
                    $"frame {measurement.Status}");
            }

            RoPacketCaptureDecodeStatus status = DecodeFrame(frame, textEncoding, out string summary);
            return new RoPacketCaptureReportEntry(
                entry.Sequence,
                entry.Direction,
                frame.PacketId,
                frame.Definition.Name,
                frame.Length,
                entry.Status,
                measurement.Status,
                status,
                summary);
        }

        private static RoPacketCaptureDecodeStatus DecodeFrame(RoPacketFrame frame, Encoding textEncoding, out string summary) {
            try {
                switch (frame.PacketId) {
                    case RoPacketIds.ZcAid:
                        RoZcAid aid = RoMapPacketParser.ParseZcAid(frame.Bytes);
                        summary = $"accountId={aid.AccountId}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcAcceptEnter:
                    case RoPacketIds.ZcAcceptEnter2:
                    case RoPacketIds.ZcAcceptEnter3:
                        RoZcAcceptEnter2 accept = RoMapPacketParser.ParseZcAcceptEnter(frame.Bytes);
                        summary = $"start={accept.StartTime} pos={FormatPosition(accept.Position)} size={accept.XSize}x{accept.YSize} font={accept.Font}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNotifyTime:
                        RoZcNotifyTime time = RoMapPacketParser.ParseZcNotifyTime(frame.Bytes);
                        summary = $"serverTime={time.ServerTime}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNotifyChat:
                        RoZcNotifyPlayerChat notifyChat = RoMapPacketParser.ParseZcNotifyChat(frame.Bytes, textEncoding);
                        summary = $"message=\"{notifyChat.Message}\"";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNotifyPlayerChat:
                        RoZcNotifyPlayerChat chat = RoMapPacketParser.ParseZcNotifyPlayerChat(frame.Bytes, textEncoding);
                        summary = $"message=\"{chat.Message}\"";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNpcAckMapMove:
                        return DecodeNpcMapMove(RoMapPacketParser.ParseZcNpcAckMapMove(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcNpcAckServerMove:
                        return DecodeNpcServerMove(RoMapPacketParser.ParseZcNpcAckServerMove(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcNpcAckServerMoveDomain:
                        return DecodeNpcServerMove(RoMapPacketParser.ParseZcNpcAckServerMoveDomain(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcSayDialog:
                        return DecodeNpcDialogText("say", RoMapPacketParser.ParseZcSayDialog(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcWaitDialog:
                        return DecodeNpcDialogSignal("wait", RoMapPacketParser.ParseZcWaitDialog(frame.Bytes), out summary);
                    case RoPacketIds.ZcCloseDialog:
                        return DecodeNpcDialogSignal("close", RoMapPacketParser.ParseZcCloseDialog(frame.Bytes), out summary);
                    case RoPacketIds.ZcClearDialog:
                        return DecodeNpcDialogSignal("clear", RoMapPacketParser.ParseZcClearDialog(frame.Bytes), out summary);
                    case RoPacketIds.ZcMenuList:
                        return DecodeNpcDialogText("menu", RoMapPacketParser.ParseZcMenuList(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcOpenEditDlg:
                        return DecodeNpcDialogSignal("number-input", RoMapPacketParser.ParseZcOpenEditDlg(frame.Bytes), out summary);
                    case RoPacketIds.ZcOpenEditDlgStr:
                        return DecodeNpcDialogSignal("text-input", RoMapPacketParser.ParseZcOpenEditDlgStr(frame.Bytes), out summary);
                    case RoPacketIds.ZcShowImage:
                        return DecodeNpcCutin(RoMapPacketParser.ParseZcShowImage(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcShowImage2:
                        return DecodeNpcCutin(RoMapPacketParser.ParseZcShowImage2(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcShowScript:
                        return DecodeNpcShowScript(RoMapPacketParser.ParseZcShowScript(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcSelectDealType:
                        RoZcSelectDealType dealType = RoMapPacketParser.ParseZcSelectDealType(frame.Bytes);
                        summary = $"npc={dealType.NpcId}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcPcPurchaseItemList:
                        RoZcNpcShopBuyList buyList = RoMapPacketParser.ParseZcPcPurchaseItemList(frame.Bytes);
                        summary = $"buyItems={buyList.Items.Length}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcPcPurchaseItemList2:
                        RoZcNpcShopBuyList buyList2 = RoMapPacketParser.ParseZcPcPurchaseItemList2(frame.Bytes);
                        summary = $"buyItems={buyList2.Items.Length}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcPcSellItemList:
                        RoZcNpcShopSellList sellList = RoMapPacketParser.ParseZcPcSellItemList(frame.Bytes);
                        summary = $"sellItems={sellList.Items.Length}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcPcPurchaseResult:
                        return DecodeNpcShopResult("buy", RoMapPacketParser.ParseZcPcPurchaseResult(frame.Bytes), out summary);
                    case RoPacketIds.ZcPcSellResult:
                        return DecodeNpcShopResult("sell", RoMapPacketParser.ParseZcPcSellResult(frame.Bytes), out summary);
                    case RoPacketIds.ZcNpcMarketOpen:
                        summary = $"marketItems={RoMapPacketParser.ParseZcNpcMarketOpen(frame.Bytes).Items.Length}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNpcMarketOpen2:
                        summary = $"marketItems={RoMapPacketParser.ParseZcNpcMarketOpen2(frame.Bytes).Items.Length}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNpcMarketPurchaseResult:
                        RoZcNpcMarketPurchaseResult marketResult = RoMapPacketParser.ParseZcNpcMarketPurchaseResult(frame.Bytes);
                        summary = $"marketResult={marketResult.Result} items={marketResult.Items.Length}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNpcMarketPurchaseResult2:
                        RoZcNpcMarketPurchaseResult marketResult2 = RoMapPacketParser.ParseZcNpcMarketPurchaseResult2(frame.Bytes);
                        summary = $"marketResult={marketResult2.Result} items={marketResult2.Items.Length}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNpcBarterMarketItemInfo:
                        summary = $"barterItems={RoMapPacketParser.ParseZcNpcBarterMarketItemInfo(frame.Bytes).Items.Length}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNpcExpandedBarterMarketItemInfo:
                        summary = $"expandedBarterItems={RoMapPacketParser.ParseZcNpcExpandedBarterMarketItemInfo(frame.Bytes).Items.Length}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNotifyStoreItemCountInfo:
                        RoZcStorageAmount storageAmount = RoMapPacketParser.ParseZcNotifyStoreItemCountInfo(frame.Bytes);
                        summary = $"storage={storageAmount.Amount}/{storageAmount.MaxAmount}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcAddItemToStore3:
                        return DecodeStorageItem(RoMapPacketParser.ParseZcAddItemToStore3(frame.Bytes), out summary);
                    case RoPacketIds.ZcDeleteItemFromStore:
                        RoZcStorageItemDelete storageDelete = RoMapPacketParser.ParseZcDeleteItemFromStore(frame.Bytes);
                        summary = $"storageIndex={storageDelete.StorageIndex} amount={storageDelete.Amount}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcCloseStore:
                        RoMapPacketParser.ParseZcCloseStore(frame.Bytes);
                        summary = "storage closed";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcDeleteItemFromBody:
                        RoZcInventoryItemDelete inventoryDelete = RoMapPacketParser.ParseZcDeleteItemFromBody(frame.Bytes);
                        summary = $"deleteType={inventoryDelete.DeleteTypeRaw} bag={inventoryDelete.BagIndex} amount={inventoryDelete.Amount}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.CzRequestChat:
                        RoZcNotifyPlayerChat requestChat = ParseCzRequestChat(frame.Bytes, textEncoding);
                        summary = $"message=\"{requestChat.Message}\"";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.CzActionRequest2:
                        DecodeCzActionRequest2(frame.Bytes, out summary);
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcChangeDirection:
                        RoZcChangeDirection direction = RoMapPacketParser.ParseZcChangeDirection(frame.Bytes);
                        summary = $"aid={direction.Aid} dir={direction.DirectionRaw} headDir={direction.HeadDirection}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcSpriteChange:
                        return DecodeSpriteChange(RoMapPacketParser.ParseZcSpriteChange(frame.Bytes), out summary);
                    case RoPacketIds.ZcSpriteChange2:
                        return DecodeSpriteChange(RoMapPacketParser.ParseZcSpriteChange2(frame.Bytes), out summary);
                    case RoPacketIds.ZcItemEntry:
                        return DecodeItemEntry(RoMapPacketParser.ParseZcItemEntry(frame.Bytes), out summary);
                    case RoPacketIds.ZcItemFallEntry:
                        return DecodeItemEntry(RoMapPacketParser.ParseZcItemFallEntry(frame.Bytes), out summary);
                    case RoPacketIds.ZcItemFallEntry2:
                        return DecodeItemEntry(RoMapPacketParser.ParseZcItemFallEntry2(frame.Bytes), out summary);
                    case RoPacketIds.ZcItemFallEntry5:
                        return DecodeItemEntry(RoMapPacketParser.ParseZcItemFallEntry5(frame.Bytes), out summary);
                    case RoPacketIds.ZcItemDisappear:
                        RoZcItemDisappear itemDisappear = RoMapPacketParser.ParseZcItemDisappear(frame.Bytes);
                        summary = $"itemAid={itemDisappear.ItemAid}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcItemThrowAck:
                        RoZcItemThrowAck throwAck = RoMapPacketParser.ParseZcItemThrowAck(frame.Bytes);
                        summary = $"bag={throwAck.BagIndex} count={throwAck.Count} success={throwAck.IsSuccess}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcItemPickupAck7:
                        RoZcItemPickupAck pickupAck = RoMapPacketParser.ParseZcItemPickupAck7(frame.Bytes);
                        summary = $"bag={pickupAck.BagIndex} item={pickupAck.ItemId} count={pickupAck.Count} result={pickupAck.Result}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcItemPickupAck8:
                        RoZcItemPickupAck pickupAck8 = RoMapPacketParser.ParseZcItemPickupAck8(frame.Bytes);
                        summary = $"bag={pickupAck8.BagIndex} item={pickupAck8.ItemId} count={pickupAck8.Count} result={pickupAck8.Result}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcUseItemAck:
                        RoZcUseItemAck useAck = RoMapPacketParser.ParseZcUseItemAck(frame.Bytes);
                        summary = $"bag={useAck.BagIndex} count={useAck.Count} result={useAck.Result}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcUseItemAck2:
                        RoZcUseItemAck useAck2 = RoMapPacketParser.ParseZcUseItemAck2(frame.Bytes);
                        summary = $"bag={useAck2.BagIndex} item={useAck2.ItemId} aid={useAck2.ActorId} count={useAck2.Count} result={useAck2.Result}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNotifyAct:
                        return DecodeActorAction(RoMapPacketParser.ParseZcNotifyAct(frame.Bytes), out summary);
                    case RoPacketIds.ZcNotifyAct2:
                        return DecodeActorAction(RoMapPacketParser.ParseZcNotifyAct2(frame.Bytes), out summary);
                    case RoPacketIds.ZcNotifyAct3:
                        return DecodeActorAction(RoMapPacketParser.ParseZcNotifyAct3(frame.Bytes), out summary);
                    case RoPacketIds.ZcEmotion:
                        RoZcEmotion emotion = RoMapPacketParser.ParseZcEmotion(frame.Bytes);
                        summary = $"gid={emotion.Gid} emotion={emotion.EmotionType}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcMsg:
                        RoZcMessage message = RoMapPacketParser.ParseZcMsg(frame.Bytes);
                        summary = $"messageId={message.MessageId}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcMsgStateChange:
                        return DecodeStatusChange(RoMapPacketParser.ParseZcMsgStateChange(frame.Bytes), out summary);
                    case RoPacketIds.ZcMsgStateChange2:
                        return DecodeStatusChange(RoMapPacketParser.ParseZcMsgStateChange2(frame.Bytes), out summary);
                    case RoPacketIds.ZcMsgStateChangeTimed:
                        return DecodeStatusChange(RoMapPacketParser.ParseZcMsgStateChangeTimed(frame.Bytes), out summary);
                    case RoPacketIds.ZcMsgStateChange3:
                        return DecodeStatusChange(RoMapPacketParser.ParseZcMsgStateChange3(frame.Bytes), out summary);
                    case RoPacketIds.ZcMsgStateChangeTimedWithTotal:
                        return DecodeStatusChange(RoMapPacketParser.ParseZcMsgStateChangeTimedWithTotal(frame.Bytes), out summary);
                    case RoPacketIds.ZcNotifyEffect:
                        DecodeNotifyEffect(frame.Bytes, out summary);
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcResurrection:
                        summary = "resurrection";
                        return RoPacketCaptureDecodeStatus.Framed;
                    case RoPacketIds.ZcHighJump:
                        summary = "highjump";
                        return RoPacketCaptureDecodeStatus.Framed;
                    case RoPacketIds.ZcNotifyNewEntry11:
                        return DecodeActorEntry(RoMapPacketParser.ParseZcNotifyNewEntry11(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcNotifyStandEntry11:
                        return DecodeActorEntry(RoMapPacketParser.ParseZcNotifyStandEntry11(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcNotifyMoveEntry11:
                        return DecodeActorEntry(RoMapPacketParser.ParseZcNotifyMoveEntry11(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcAckReqName:
                        return DecodeActorIdentity(RoMapPacketParser.ParseZcAckReqName(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcAckReqNameAll:
                        return DecodeActorIdentity(RoMapPacketParser.ParseZcAckReqNameAll(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcAckReqNameAll2:
                        return DecodeActorIdentity(RoMapPacketParser.ParseZcAckReqNameAll2(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcAckReqNameAllNpc:
                        return DecodeActorIdentity(RoMapPacketParser.ParseZcAckReqNameAllNpc(frame.Bytes, textEncoding), out summary);
                    case RoPacketIds.ZcNotifyMove:
                        RoZcNotifyMove move = RoMapPacketParser.ParseZcNotifyMove(frame.Bytes);
                        summary = $"gid={move.Gid} move={FormatPath(move.MovePath)} startTime={move.MoveStartTime}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNotifyPlayerMove:
                        RoZcNotifyPlayerMove playerMove = RoMapPacketParser.ParseZcNotifyPlayerMove(frame.Bytes);
                        summary = $"move={FormatPath(playerMove.MovePath)} startTime={playerMove.MoveStartTime}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcStopMove:
                        RoZcStopMove stopMove = RoMapPacketParser.ParseZcStopMove(frame.Bytes);
                        summary = $"aid={stopMove.Aid} pos={stopMove.X},{stopMove.Y}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNotifyVanish:
                        RoZcNotifyVanish vanish = RoMapPacketParser.ParseZcNotifyVanish(frame.Bytes);
                        summary = $"gid={vanish.Gid} type={vanish.Type} raw={vanish.TypeRaw}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcAttackFailureForDistance:
                        DecodeAttackFailureForDistance(frame.Bytes, out summary);
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcActionFailure:
                        RoZcActionFailure actionFailure = RoMapPacketParser.ParseZcActionFailure(frame.Bytes);
                        summary = $"type={actionFailure.TypeRaw}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcEquipArrow:
                        RoZcEquipArrow equipArrow = RoMapPacketParser.ParseZcEquipArrow(frame.Bytes);
                        summary = $"bag={equipArrow.BagIndex}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcHpInfo:
                        DecodeHpInfo(frame.Bytes, out summary);
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNotifyExp2:
                        DecodeNotifyExp2(frame.Bytes, out summary);
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcRecovery2:
                        RoZcRecovery2 recovery = RoMapPacketParser.ParseZcRecovery2(frame.Bytes);
                        summary = $"recovery var={recovery.PropertyRaw} amount={recovery.Amount}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcAckToUseSkill:
                        RoZcAckToUseSkill skillUseAck = RoMapPacketParser.ParseZcAckToUseSkill(frame.Bytes);
                        summary = $"skill={skillUseAck.SkillId} value={skillUseAck.Value} item={skillUseAck.ItemId} result={skillUseAck.ResultRaw} cause={skillUseAck.CauseRaw}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcUseSkillAckLegacy:
                        return DecodeSkillCast(RoMapPacketParser.ParseZcUseSkillAckLegacy(frame.Bytes), out summary);
                    case RoPacketIds.ZcUseSkillAck2:
                        return DecodeSkillCast(RoMapPacketParser.ParseZcUseSkillAck2(frame.Bytes), out summary);
                    case RoPacketIds.ZcUseSkillAck3:
                        return DecodeSkillCast(RoMapPacketParser.ParseZcUseSkillAck3(frame.Bytes), out summary);
                    case RoPacketIds.ZcNotifySkillLegacy:
                        return DecodeSkillDamage(RoMapPacketParser.ParseZcNotifySkillLegacy(frame.Bytes), out summary);
                    case RoPacketIds.ZcNotifySkill2:
                        return DecodeSkillDamage(RoMapPacketParser.ParseZcNotifySkill2(frame.Bytes), out summary);
                    case RoPacketIds.ZcUseSkill:
                        RoZcUseSkillResult skillResult = RoMapPacketParser.ParseZcUseSkill(frame.Bytes);
                        summary = $"skill={skillResult.SkillId} src={skillResult.SourceId} target={skillResult.TargetId} value={skillResult.Value} result={skillResult.ResultRaw}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcReserved0306:
                        summary = "reserved";
                        return RoPacketCaptureDecodeStatus.Framed;
                    case RoPacketIds.ZcNotifyGroundSkill:
                        RoZcNotifyGroundSkill groundSkill = RoMapPacketParser.ParseZcNotifyGroundSkill(frame.Bytes);
                        summary = $"skill={groundSkill.SkillId} src={groundSkill.SourceId} level={groundSkill.Level} pos={groundSkill.X},{groundSkill.Y} tick={groundSkill.ServerTick}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcSkillDisappear:
                        RoZcSkillDisappear skillDisappear = RoMapPacketParser.ParseZcSkillDisappear(frame.Bytes);
                        summary = $"unit={skillDisappear.UnitId}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcDispel:
                        summary = $"actor={RoMapPacketParser.ParseZcDispel(frame.Bytes).ActorId}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNotifyEffect2:
                    case RoPacketIds.ZcNotifyEffect3:
                        RoZcNotifyEffectById effectById = frame.PacketId == RoPacketIds.ZcNotifyEffect2
                            ? RoMapPacketParser.ParseZcNotifyEffect2(frame.Bytes)
                            : RoMapPacketParser.ParseZcNotifyEffect3(frame.Bytes);
                        summary = $"actor={effectById.ActorId} effect={effectById.EffectId} value={effectById.Value}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNpcShowEfstUpdate:
                        RoZcOptionChange2 optionChange2 = RoMapPacketParser.ParseZcNpcShowEfstUpdate(frame.Bytes);
                        summary = $"actor={optionChange2.ActorId} option=0x{optionChange2.Option:x} opt3=0x{optionChange2.Opt3:x}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcSkillPostDelay:
                        RoZcSkillPostDelay postDelay = RoMapPacketParser.ParseZcSkillPostDelay(frame.Bytes);
                        summary = $"skill={postDelay.SkillId} delay={postDelay.DelayTime}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcNotifySkillPosition:
                        RoZcNotifySkillPosition skillPosition = RoMapPacketParser.ParseZcNotifySkillPosition(frame.Bytes);
                        summary = $"skill={skillPosition.SkillId} src={skillPosition.SourceId} target={skillPosition.TargetId} pos={skillPosition.X},{skillPosition.Y} damage={skillPosition.Damage} hits={skillPosition.HitCount} action={skillPosition.ActionRaw}";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    case RoPacketIds.ZcSkillEntry:
                        return DecodeSkillEntry(RoMapPacketParser.ParseZcSkillEntry(frame.Bytes), out summary);
                    case RoPacketIds.ZcSkillEntry2:
                        return DecodeSkillEntry(RoMapPacketParser.ParseZcSkillEntry2(frame.Bytes), out summary);
                    case RoPacketIds.ZcSkillEntry3:
                        return DecodeSkillEntry(RoMapPacketParser.ParseZcSkillEntry3(frame.Bytes), out summary);
                    case RoPacketIds.ZcSkillEntry4:
                        return DecodeSkillEntry(RoMapPacketParser.ParseZcSkillEntry4(frame.Bytes), out summary);
                    case RoPacketIds.ZcSkillEntry5:
                        return DecodeSkillEntry(RoMapPacketParser.ParseZcSkillEntry5(frame.Bytes), out summary);
                    case RoPacketIds.CzNotifyActorInit:
                        summary = "load-end ack";
                        return RoPacketCaptureDecodeStatus.Decoded;
                    default:
                        summary = frame.Definition.Name;
                        return RoPacketCaptureDecodeStatus.Framed;
                }
            } catch (Exception ex) {
                summary = $"{frame.Definition.Name} decode failed: {ex.Message}";
                return RoPacketCaptureDecodeStatus.DecodeFailed;
            }
        }

        private static RoZcNotifyPlayerChat ParseCzRequestChat(byte[] frame, Encoding textEncoding) {
            var reader = new RoPacketReader(frame);
            ushort packetId = reader.ReadUInt16();
            if (packetId != RoPacketIds.CzRequestChat) {
                throw new InvalidDataException($"Expected CZ_REQUEST_CHAT, got 0x{packetId:x4}.");
            }

            int packetLength = reader.ReadUInt16();
            if (packetLength != frame.Length) {
                throw new InvalidDataException($"CZ_REQUEST_CHAT length {packetLength} does not match frame length {frame.Length}.");
            }

            return new RoZcNotifyPlayerChat(reader.ReadFixedString(packetLength - 4, textEncoding));
        }

        private static void DecodeCzActionRequest2(byte[] frame, out string summary) {
            var reader = new RoPacketReader(frame);
            ushort packetId = reader.ReadUInt16();
            if (packetId != RoPacketIds.CzActionRequest2) {
                throw new InvalidDataException($"Expected CZ_REQUEST_ACT2, got 0x{packetId:x4}.");
            }

            uint targetAid = reader.ReadUInt32();
            byte action = reader.ReadByte();
            summary = $"target={targetAid} action={action}";
        }

        private static RoPacketCaptureDecodeStatus DecodeActorEntry(RoZcNotifyActorEntry actor, out string summary) {
            string location = actor.HasMovePath ? FormatPath(actor.MovePath) : FormatPosition(actor.Position);
            summary = $"kind={actor.Kind} aid={actor.Aid} gid={actor.Gid} type={actor.ObjectType} job={actor.Job} loc={location} state={actor.State} name=\"{actor.Name}\"";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeActorIdentity(RoZcActorIdentity identity, out string summary) {
            string suffix = string.Empty;
            if (!string.IsNullOrWhiteSpace(identity.PartyName)) {
                suffix += $" party=\"{identity.PartyName}\"";
            }

            if (!string.IsNullOrWhiteSpace(identity.GuildName)) {
                suffix += $" guild=\"{identity.GuildName}\"";
            }

            if (!string.IsNullOrWhiteSpace(identity.GuildPositionName)) {
                suffix += $" position=\"{identity.GuildPositionName}\"";
            }

            if (!string.IsNullOrWhiteSpace(identity.TitleName)) {
                suffix += $" title=\"{identity.TitleName}\"";
            } else if (identity.HasTitleId) {
                suffix += $" titleId={identity.TitleId}";
            }

            if (identity.HasGroupId) {
                suffix += $" group={identity.GroupId}";
            }

            summary = $"gid={identity.ActorId} name=\"{identity.Name}\"{suffix}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static void DecodeHpInfo(byte[] frame, out string summary) {
            RoZcHpInfo hp = RoMapPacketParser.ParseZcHpInfo(frame);
            summary = $"id={hp.ActorId} hp={hp.Hp}/{hp.MaxHp}";
        }

        private static void DecodeAttackFailureForDistance(byte[] frame, out string summary) {
            RoZcAttackFailureForDistance packet = RoMapPacketParser.ParseZcAttackFailureForDistance(frame);
            summary = $"target={packet.TargetAid} targetPos={packet.TargetX},{packet.TargetY} pos={packet.X},{packet.Y} range={packet.Range}";
        }

        private static RoPacketCaptureDecodeStatus DecodeSpriteChange(RoZcSpriteChange change, out string summary) {
            summary = $"gid={change.Gid} type={change.Type} rawType={change.TypeRaw} value={change.Value} packed={change.PacksWeaponAndShield}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeItemEntry(RoZcItemEntry item, out string summary) {
            summary = $"itemAid={item.ItemAid} itemId={item.ItemId} count={item.Count} identified={item.IsIdentified} pos={item.X},{item.Y} sub={item.SubX},{item.SubY} falling={item.IsFalling}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeNpcDialogText(string action, RoZcNpcDialogText dialog, out string summary) {
            summary = $"npc={dialog.NpcId} action={action} text=\"{dialog.Message}\"";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeNpcDialogSignal(string action, RoZcNpcDialogSignal dialog, out string summary) {
            summary = $"npc={dialog.NpcId} action={action}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeNpcMapMove(RoZcNpcMapMove mapMove, out string summary) {
            summary = $"map={mapMove.MapName} pos={mapMove.X},{mapMove.Y}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeNpcServerMove(RoZcNpcServerMove serverMove, out string summary) {
            string host = string.IsNullOrWhiteSpace(serverMove.Domain) ? serverMove.Address : serverMove.Domain;
            summary = $"map={serverMove.MapName} pos={serverMove.X},{serverMove.Y} host={host}:{serverMove.Port}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeNpcCutin(RoZcNpcCutin cutin, out string summary) {
            summary = cutin.IsClear
                ? $"cutin clear image={cutin.ImageName}"
                : $"cutin image={cutin.ImageName} type={cutin.TypeRaw}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeNpcShowScript(RoZcShowScript showScript, out string summary) {
            summary = $"entity={showScript.EntityId} text=\"{showScript.Message}\"";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeNpcShopResult(string action, RoZcNpcShopResult result, out string summary) {
            summary = $"{action} result={result.Result} success={result.IsSuccess}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeStorageItem(RoZcStorageItem item, out string summary) {
            summary = $"storageIndex={item.StorageIndex} item={item.ItemId} amount={item.Amount} identified={item.IsIdentified} refine={item.RefineLevel}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeActorAction(RoZcNotifyAct action, out string summary) {
            summary = $"src={action.SourceId} dst={action.TargetId} action={action.Action} raw={action.ActionRaw} damage={action.Damage} hits={action.HitCount} damage2={action.Damage2} sp={action.IsSpDamage}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeSkillCast(RoZcUseSkillAck skill, out string summary) {
            summary = $"skill={skill.SkillId} src={skill.SourceId} target={skill.TargetId} pos={skill.X},{skill.Y} element={skill.Element} delay={skill.DelayTime} disposable={skill.Disposable} attackMT={skill.AttackMotionTime}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeSkillDamage(RoZcNotifySkill skill, out string summary) {
            summary = $"skill={skill.SkillId} src={skill.SourceId} target={skill.TargetId} damage={skill.Damage} level={skill.Level} hits={skill.HitCount} action={skill.ActionRaw} delay={skill.SourceDelay}/{skill.TargetDelay}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeSkillEntry(RoZcSkillEntry skillEntry, out string summary) {
            summary = $"skill={skillEntry.SkillId} creator={skillEntry.CreatorId} unit={skillEntry.UnitId} pos={skillEntry.X},{skillEntry.Y} range={skillEntry.Range} visible={skillEntry.Visible} level={skillEntry.SkillLevel}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static RoPacketCaptureDecodeStatus DecodeStatusChange(RoZcMsgStateChange status, out string summary) {
            summary = $"aid={status.Aid} status={status.StatusId} active={status.Active} remain={status.RemainingMilliseconds} total={status.TotalMilliseconds}";
            return RoPacketCaptureDecodeStatus.Decoded;
        }

        private static void DecodeNotifyEffect(byte[] frame, out string summary) {
            var reader = new RoPacketReader(frame);
            ushort packetId = reader.ReadUInt16();
            if (packetId != RoPacketIds.ZcNotifyEffect) {
                throw new InvalidDataException($"Expected ZC_NOTIFY_EFFECT, got 0x{packetId:x4}.");
            }

            uint id = reader.ReadUInt32();
            int effectId = reader.ReadInt32();
            summary = $"id={id} effect={effectId}";
        }

        private static void DecodeNotifyExp2(byte[] frame, out string summary) {
            var reader = new RoPacketReader(frame);
            ushort packetId = reader.ReadUInt16();
            if (packetId != RoPacketIds.ZcNotifyExp2) {
                throw new InvalidDataException($"Expected ZC_NOTIFY_EXP2, got 0x{packetId:x4}.");
            }

            uint accountId = reader.ReadUInt32();
            long amount = reader.ReadInt64();
            ushort varId = reader.ReadUInt16();
            ushort expType = reader.ReadUInt16();
            summary = $"account={accountId} amount={amount} var={varId} type={expType}";
        }

        private static string FormatPosition(RoPacketPosition position) {
            return $"{position.X},{position.Y},{position.Direction}";
        }

        private static string FormatPath(RoPacketPositionPair path) {
            return $"{path.StartX},{path.StartY}->{path.EndX},{path.EndY}";
        }
    }

    public sealed class RoLiveCaptureRequest {
        public RoLiveCaptureRequest(string account, string password, string characterName) {
            Account = account ?? string.Empty;
            Password = password ?? string.Empty;
            CharacterName = characterName ?? string.Empty;
            Profile = RoServerProfile.LocalRathena20200401();
            TimeoutMilliseconds = 5000;
            CaptureCapacity = 8192;
            HairStyle = 1;
            MoveX = -1;
            MoveY = -1;
            MoveDirection = 6;
        }

        public RoServerProfile Profile { get; set; }
        public string Account { get; set; }
        public string Password { get; set; }
        public string CharacterName { get; set; }
        public int CharacterSlot { get; set; }
        public ushort HairColor { get; set; }
        public ushort HairStyle { get; set; }
        public int StartJob { get; set; }
        public byte? Sex { get; set; }
        public bool RequestMove { get; set; }
        public int MoveX { get; set; }
        public int MoveY { get; set; }
        public int MoveDirection { get; set; }
        public int TimeoutMilliseconds { get; set; }
        public int CaptureCapacity { get; set; }
        public bool RedactSensitivePackets { get; set; } = true;
        public string CapturePath { get; set; }
        public string ReportPath { get; set; }
    }

    public sealed class RoLiveCaptureResult {
        public RoLiveCaptureResult(
            RoLoginEnterMapResult session,
            RoPacketCaptureDocument capture,
            RoPacketCaptureReport report,
            string capturePath,
            string reportPath) {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            Capture = capture ?? throw new ArgumentNullException(nameof(capture));
            Report = report ?? throw new ArgumentNullException(nameof(report));
            CapturePath = capturePath ?? string.Empty;
            ReportPath = reportPath ?? string.Empty;
        }

        public RoLoginEnterMapResult Session { get; }
        public RoPacketCaptureDocument Capture { get; }
        public RoPacketCaptureReport Report { get; }
        public string CapturePath { get; }
        public string ReportPath { get; }
    }

    public static class RoLiveCaptureRunner {
        public static RoLiveCaptureResult RunLoginCreateEnterLoadAndOptionalMove(RoLiveCaptureRequest request) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.Account)) {
                throw new ArgumentException("Account is required.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.CharacterName)) {
                throw new ArgumentException("Character name is required.", nameof(request));
            }

            if (request.RequestMove && ((request.MoveX < 0) != (request.MoveY < 0))) {
                throw new ArgumentException("MoveX and MoveY must both be non-negative explicit cell coordinates, or both negative for the default relative move.", nameof(request));
            }

            IRoPacketProfile packetProfile = RoPacketProfiles.ForServerProfile(request.Profile);
            RoPacketRegistry registry = packetProfile.CreateRegistry();
            var captureBuffer = new RoPacketCaptureBuffer(request.CaptureCapacity <= 0 ? 8192 : request.CaptureCapacity);
            var client = new RoLoginSessionClient(
                request.Profile,
                registry,
                captureSink: captureBuffer,
                timeoutMilliseconds: request.TimeoutMilliseconds <= 0 ? 5000 : request.TimeoutMilliseconds);

            RoLoginEnterMapResult session = request.RequestMove
                ? client.LoginCreateCharacterSelectEnterMapLoadInitialStateAndRequestMove(
                    request.Account,
                    request.Password,
                    request.CharacterName,
                    request.MoveX,
                    request.MoveY,
                    request.MoveDirection,
                    request.CharacterSlot,
                    request.HairColor,
                    request.HairStyle,
                    request.StartJob,
                    request.Sex)
                : client.LoginCreateCharacterSelectEnterMapAndLoadInitialState(
                    request.Account,
                    request.Password,
                    request.CharacterName,
                    request.CharacterSlot,
                    request.HairColor,
                    request.HairStyle,
                    request.StartJob,
                    request.Sex);

            RoPacketCaptureDocument capture = captureBuffer.ToDocument(request.Profile);
            if (request.RedactSensitivePackets) {
                capture = RoPacketCaptureSanitizer.RedactSensitivePackets(capture);
            }

            RoPacketCaptureReport report = RoPacketCaptureReporter.Build(capture, registry);

            if (!string.IsNullOrWhiteSpace(request.CapturePath)) {
                RoPacketCaptureCodec.Save(request.CapturePath, capture);
            }

            if (!string.IsNullOrWhiteSpace(request.ReportPath)) {
                File.WriteAllText(request.ReportPath, report.ToText(), Encoding.UTF8);
            }

            return new RoLiveCaptureResult(session, capture, report, request.CapturePath, request.ReportPath);
        }
    }

    public static class RoPacketCaptureSanitizer {
        private const int CaLoginAccountOffset = 6;
        private const int CaLoginAccountLength = 24;
        private const int CaLoginPasswordOffset = 30;
        private const int CaLoginPasswordLength = 24;

        public static RoPacketCaptureDocument RedactSensitivePackets(RoPacketCaptureDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            var entries = new RoPacketCaptureEntry[document.Entries.Length];
            for (int i = 0; i < document.Entries.Length; i++) {
                RoPacketCaptureEntry entry = document.Entries[i];
                entries[i] = RedactEntry(entry);
            }

            return new RoPacketCaptureDocument(document.ProfileName, document.PacketVersion, document.CreatedAt, entries);
        }

        private static RoPacketCaptureEntry RedactEntry(RoPacketCaptureEntry entry) {
            if (entry == null) {
                return null;
            }

            byte[] bytes = entry.CloneBytes();
            if (entry.Direction == RoPacketDirection.ClientToServer &&
                entry.PacketId == RoPacketIds.CaLogin &&
                bytes.Length >= CaLoginPasswordOffset + CaLoginPasswordLength) {
                RedactFixedString(bytes, CaLoginAccountOffset, CaLoginAccountLength, "redacted");
                RedactFixedString(bytes, CaLoginPasswordOffset, CaLoginPasswordLength, string.Empty);
            }

            return new RoPacketCaptureEntry(
                entry.Sequence,
                entry.Timestamp,
                entry.Direction,
                entry.PacketId,
                entry.PacketName,
                bytes,
                entry.Status);
        }

        private static void RedactFixedString(byte[] bytes, int offset, int length, string replacement) {
            Array.Clear(bytes, offset, length);
            if (string.IsNullOrEmpty(replacement)) {
                return;
            }

            byte[] replacementBytes = Encoding.ASCII.GetBytes(replacement);
            Array.Copy(replacementBytes, 0, bytes, offset, Math.Min(replacementBytes.Length, length));
        }
    }

    public static class RoPacketCaptureCodec {
        private const string Header = "ROPCAP1";

        public static string Encode(RoPacketCaptureDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            var builder = new StringBuilder();
            builder.AppendLine(Header);
            AppendMeta(builder, "profile", document.ProfileName);
            AppendMeta(builder, "packetver", document.PacketVersion.ToString(CultureInfo.InvariantCulture));
            AppendMeta(builder, "createdUtc", document.CreatedAt.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture));

            foreach (RoPacketCaptureEntry entry in document.Entries) {
                builder.Append("packet");
                builder.Append('\t');
                builder.Append(entry.Sequence.ToString(CultureInfo.InvariantCulture));
                builder.Append('\t');
                builder.Append(entry.Timestamp.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture));
                builder.Append('\t');
                builder.Append(entry.Direction);
                builder.Append('\t');
                builder.Append(entry.PacketId.ToString("x4", CultureInfo.InvariantCulture));
                builder.Append('\t');
                builder.Append(Escape(entry.PacketName));
                builder.Append('\t');
                builder.Append(entry.Status);
                builder.Append('\t');
                builder.Append(Convert.ToBase64String(entry.Bytes));
                builder.AppendLine();
            }

            return builder.ToString();
        }

        public static RoPacketCaptureDocument Decode(string text) {
            if (text == null) {
                throw new ArgumentNullException(nameof(text));
            }

            using (var reader = new StringReader(text)) {
                string first = reader.ReadLine();
                if (!string.Equals(first, Header, StringComparison.Ordinal)) {
                    throw new FormatException("Packet capture header is missing or unsupported.");
                }

                string profileName = string.Empty;
                int packetVersion = 0;
                DateTimeOffset createdAt = DateTimeOffset.UtcNow;
                var entries = new List<RoPacketCaptureEntry>();
                string line;
                int lineNumber = 1;

                while ((line = reader.ReadLine()) != null) {
                    lineNumber++;
                    if (line.Length == 0) {
                        continue;
                    }

                    string[] parts = line.Split('\t');
                    if (parts.Length < 3) {
                        throw new FormatException($"Packet capture line {lineNumber} is incomplete.");
                    }

                    if (string.Equals(parts[0], "meta", StringComparison.Ordinal)) {
                        string key = parts[1];
                        string value = Unescape(parts[2]);
                        if (string.Equals(key, "profile", StringComparison.Ordinal)) {
                            profileName = value;
                        } else if (string.Equals(key, "packetver", StringComparison.Ordinal)) {
                            packetVersion = int.Parse(value, CultureInfo.InvariantCulture);
                        } else if (string.Equals(key, "createdUtc", StringComparison.Ordinal)) {
                            createdAt = FromUtcTicks(long.Parse(value, CultureInfo.InvariantCulture));
                        }

                        continue;
                    }

                    if (!string.Equals(parts[0], "packet", StringComparison.Ordinal) || parts.Length != 8) {
                        throw new FormatException($"Packet capture line {lineNumber} is not a packet entry.");
                    }

                    long sequence = long.Parse(parts[1], CultureInfo.InvariantCulture);
                    DateTimeOffset timestamp = FromUtcTicks(long.Parse(parts[2], CultureInfo.InvariantCulture));
                    var direction = (RoPacketDirection) Enum.Parse(typeof(RoPacketDirection), parts[3]);
                    ushort packetId = ushort.Parse(parts[4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    string packetName = Unescape(parts[5]);
                    var status = (RoPacketFrameStatus) Enum.Parse(typeof(RoPacketFrameStatus), parts[6]);
                    byte[] bytes = Convert.FromBase64String(parts[7]);
                    entries.Add(new RoPacketCaptureEntry(sequence, timestamp, direction, packetId, packetName, bytes, status));
                }

                return new RoPacketCaptureDocument(profileName, packetVersion, createdAt, entries);
            }
        }

        public static void Save(string path, RoPacketCaptureDocument document) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Capture path is required.", nameof(path));
            }

            File.WriteAllText(path, Encode(document), Encoding.UTF8);
        }

        public static RoPacketCaptureDocument Load(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Capture path is required.", nameof(path));
            }

            return Decode(File.ReadAllText(path, Encoding.UTF8));
        }

        private static void AppendMeta(StringBuilder builder, string key, string value) {
            builder.Append("meta");
            builder.Append('\t');
            builder.Append(key);
            builder.Append('\t');
            builder.Append(Escape(value ?? string.Empty));
            builder.AppendLine();
        }

        private static DateTimeOffset FromUtcTicks(long ticks) {
            return new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
        }

        private static string Escape(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++) {
                char c = value[i];
                switch (c) {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }

        private static string Unescape(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            bool escaped = false;
            for (int i = 0; i < value.Length; i++) {
                char c = value[i];
                if (!escaped) {
                    if (c == '\\') {
                        escaped = true;
                    } else {
                        builder.Append(c);
                    }

                    continue;
                }

                switch (c) {
                    case '\\':
                        builder.Append('\\');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    default:
                        builder.Append(c);
                        break;
                }

                escaped = false;
            }

            if (escaped) {
                builder.Append('\\');
            }

            return builder.ToString();
        }
    }

    public static class RoLogCaptureCodec {
        private const string Header = "ROLOG1";
        private const string LegacyHeader = "ROPCAP1";

        public static string Encode(RoPacketCaptureDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            var builder = new StringBuilder();
            builder.AppendLine(Header);
            AppendMeta(builder, "profile", document.ProfileName);
            AppendMeta(builder, "packetver", document.PacketVersion.ToString(CultureInfo.InvariantCulture));
            AppendMeta(builder, "createdUtc", document.CreatedAt.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture));
            AppendMeta(builder, "bytesPolicy", "wire");

            foreach (RoPacketCaptureEntry entry in document.Entries) {
                builder.Append("packet");
                builder.Append('\t');
                builder.Append(entry.Sequence.ToString(CultureInfo.InvariantCulture));
                builder.Append('\t');
                builder.Append(entry.Timestamp.UtcDateTime.Ticks.ToString(CultureInfo.InvariantCulture));
                builder.Append('\t');
                builder.Append(entry.Direction);
                builder.Append('\t');
                builder.Append(entry.PacketId.ToString("x4", CultureInfo.InvariantCulture));
                builder.Append('\t');
                builder.Append(Escape(entry.PacketName));
                builder.Append('\t');
                builder.Append(entry.Status);
                builder.Append('\t');
                builder.Append(Convert.ToBase64String(entry.Bytes));
                builder.AppendLine();
            }

            return builder.ToString();
        }

        public static RoPacketCaptureDocument Decode(string text) {
            if (text == null) {
                throw new ArgumentNullException(nameof(text));
            }

            if (text.StartsWith(LegacyHeader, StringComparison.Ordinal)) {
                return RoPacketCaptureCodec.Decode(text);
            }

            using (var reader = new StringReader(text)) {
                string first = reader.ReadLine();
                if (!string.Equals(first, Header, StringComparison.Ordinal)) {
                    throw new FormatException("RO log capture header is missing or unsupported.");
                }

                string profileName = string.Empty;
                int packetVersion = 0;
                DateTimeOffset createdAt = DateTimeOffset.UtcNow;
                var entries = new List<RoPacketCaptureEntry>();
                string line;
                int lineNumber = 1;

                while ((line = reader.ReadLine()) != null) {
                    lineNumber++;
                    if (line.Length == 0) {
                        continue;
                    }

                    string[] parts = line.Split('\t');
                    if (parts.Length < 3) {
                        throw new FormatException($"RO log line {lineNumber} is incomplete.");
                    }

                    if (string.Equals(parts[0], "meta", StringComparison.Ordinal)) {
                        string key = parts[1];
                        string value = Unescape(parts[2]);
                        if (string.Equals(key, "profile", StringComparison.Ordinal)) {
                            profileName = value;
                        } else if (string.Equals(key, "packetver", StringComparison.Ordinal)) {
                            packetVersion = int.Parse(value, CultureInfo.InvariantCulture);
                        } else if (string.Equals(key, "createdUtc", StringComparison.Ordinal)) {
                            createdAt = FromUtcTicks(long.Parse(value, CultureInfo.InvariantCulture));
                        }

                        continue;
                    }

                    if (!string.Equals(parts[0], "packet", StringComparison.Ordinal) || parts.Length != 8) {
                        throw new FormatException($"RO log line {lineNumber} is not a packet entry.");
                    }

                    long sequence = long.Parse(parts[1], CultureInfo.InvariantCulture);
                    DateTimeOffset timestamp = FromUtcTicks(long.Parse(parts[2], CultureInfo.InvariantCulture));
                    var direction = (RoPacketDirection) Enum.Parse(typeof(RoPacketDirection), parts[3]);
                    ushort packetId = ushort.Parse(parts[4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    string packetName = Unescape(parts[5]);
                    var status = (RoPacketFrameStatus) Enum.Parse(typeof(RoPacketFrameStatus), parts[6]);
                    byte[] bytes = Convert.FromBase64String(parts[7]);
                    entries.Add(new RoPacketCaptureEntry(sequence, timestamp, direction, packetId, packetName, bytes, status));
                }

                return new RoPacketCaptureDocument(profileName, packetVersion, createdAt, entries);
            }
        }

        public static void Save(string path, RoPacketCaptureDocument document) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("RO log path is required.", nameof(path));
            }

            File.WriteAllText(path, Encode(document), Encoding.UTF8);
        }

        public static RoPacketCaptureDocument Load(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("RO log path is required.", nameof(path));
            }

            return Decode(File.ReadAllText(path, Encoding.UTF8));
        }

        private static void AppendMeta(StringBuilder builder, string key, string value) {
            builder.Append("meta");
            builder.Append('\t');
            builder.Append(key);
            builder.Append('\t');
            builder.Append(Escape(value ?? string.Empty));
            builder.AppendLine();
        }

        private static DateTimeOffset FromUtcTicks(long ticks) {
            return new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
        }

        private static string Escape(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++) {
                char c = value[i];
                switch (c) {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }

        private static string Unescape(string value) {
            if (string.IsNullOrEmpty(value)) {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);
            bool escaped = false;
            for (int i = 0; i < value.Length; i++) {
                char c = value[i];
                if (!escaped) {
                    if (c == '\\') {
                        escaped = true;
                    } else {
                        builder.Append(c);
                    }

                    continue;
                }

                switch (c) {
                    case '\\':
                        builder.Append('\\');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    default:
                        builder.Append(c);
                        break;
                }

                escaped = false;
            }

            if (escaped) {
                builder.Append('\\');
            }

            return builder.ToString();
        }
    }

    public enum RoPacketReplayRunnerEntryStatus {
        ReplayedFrame = 0,
        RawEntry = 1,
        UnknownPacket = 2,
        InvalidLength = 3
    }

    public sealed class RoPacketReplayRunnerEntry {
        public RoPacketReplayRunnerEntry(
            long sequence,
            RoPacketDirection direction,
            ushort packetId,
            string packetName,
            int length,
            RoPacketReplayRunnerEntryStatus status,
            RoPacketFrameStatus frameStatus) {
            Sequence = sequence;
            Direction = direction;
            PacketId = packetId;
            PacketName = packetName ?? string.Empty;
            Length = length;
            Status = status;
            FrameStatus = frameStatus;
        }

        public long Sequence { get; }
        public RoPacketDirection Direction { get; }
        public ushort PacketId { get; }
        public string PacketName { get; }
        public int Length { get; }
        public RoPacketReplayRunnerEntryStatus Status { get; }
        public RoPacketFrameStatus FrameStatus { get; }
    }

    public sealed class RoPacketReplayRunnerResult {
        public RoPacketReplayRunnerResult(IEnumerable<RoPacketReplayRunnerEntry> entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            var list = new List<RoPacketReplayRunnerEntry>();
            foreach (RoPacketReplayRunnerEntry entry in entries) {
                if (entry == null) {
                    continue;
                }

                list.Add(entry);
                switch (entry.Status) {
                    case RoPacketReplayRunnerEntryStatus.ReplayedFrame:
                        ReplayedFrameCount++;
                        break;
                    case RoPacketReplayRunnerEntryStatus.RawEntry:
                        RawEntryCount++;
                        break;
                    case RoPacketReplayRunnerEntryStatus.UnknownPacket:
                        UnknownPacketCount++;
                        break;
                    case RoPacketReplayRunnerEntryStatus.InvalidLength:
                        InvalidLengthCount++;
                        break;
                }
            }

            Entries = list.ToArray();
        }

        public RoPacketReplayRunnerEntry[] Entries { get; }
        public int Count => Entries.Length;
        public int ReplayedFrameCount { get; }
        public int RawEntryCount { get; }
        public int UnknownPacketCount { get; }
        public int InvalidLengthCount { get; }
        public bool HasProblems => UnknownPacketCount > 0 || InvalidLengthCount > 0;
    }

    public sealed class RoPacketReplayRunner {
        private readonly RoServerProfile profile;
        private readonly bool hasProfile;
        private readonly RoPacketRegistry registry;
        private readonly IRoPacketTransform transform;

        public RoPacketReplayRunner(IRoPacketProfile packetProfile)
            : this(packetProfile, packetProfile?.CreateRegistry(), packetProfile?.CreatePacketTransform()) {
        }

        public RoPacketReplayRunner(RoPacketRegistry registry) {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.transform = RoNoPacketTransform.Instance;
        }

        private RoPacketReplayRunner(
            IRoPacketProfile packetProfile,
            RoPacketRegistry registry,
            IRoPacketTransform transform) {
            if (packetProfile == null) {
                throw new ArgumentNullException(nameof(packetProfile));
            }

            this.profile = ToServerProfile(packetProfile);
            this.hasProfile = true;
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.transform = transform ?? RoNoPacketTransform.Instance;
        }

        public RoPacketReplayRunnerResult Run(RoPacketCaptureDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            if (hasProfile) {
                RoPacketCaptureProfileResolver.ValidateDocumentMatchesProfile(document, profile);
            }

            if (transform is IRoPacketTransformLifecycle lifecycle) {
                lifecycle.Reset(profile);
            }

            var entries = new List<RoPacketReplayRunnerEntry>();
            foreach (RoPacketCaptureEntry entry in document.Entries) {
                entries.Add(ReplayEntry(entry));
            }

            return new RoPacketReplayRunnerResult(entries);
        }

        public RoPacketReplayRunnerResult RunLogFile(string path) {
            return Run(RoLogCaptureCodec.Load(path));
        }

        private RoPacketReplayRunnerEntry ReplayEntry(RoPacketCaptureEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }

            if (!entry.IsPacketFrame || entry.Bytes.Length < 2) {
                return new RoPacketReplayRunnerEntry(
                    entry.Sequence,
                    entry.Direction,
                    entry.PacketId,
                    entry.PacketName,
                    entry.Length,
                    RoPacketReplayRunnerEntryStatus.RawEntry,
                    entry.Status);
            }

            byte[] bytes = entry.CloneBytes();
            if (entry.Direction == RoPacketDirection.ClientToServer && transform != null) {
                transform.TransformOutbound(bytes, 0, bytes.Length, profile);
            }

            RoPacketFrameMeasurement measurement = registry.MeasureFrame(
                bytes,
                0,
                bytes.Length,
                entry.Direction);

            if (measurement.Status == RoPacketFrameStatus.Complete) {
                return new RoPacketReplayRunnerEntry(
                    entry.Sequence,
                    entry.Direction,
                    measurement.PacketId,
                    measurement.Definition.Name,
                    measurement.FrameLength,
                    RoPacketReplayRunnerEntryStatus.ReplayedFrame,
                    measurement.Status);
            }

            RoPacketReplayRunnerEntryStatus status = measurement.Status == RoPacketFrameStatus.UnknownPacket
                ? RoPacketReplayRunnerEntryStatus.UnknownPacket
                : RoPacketReplayRunnerEntryStatus.InvalidLength;
            return new RoPacketReplayRunnerEntry(
                entry.Sequence,
                entry.Direction,
                measurement.PacketId,
                entry.PacketName,
                entry.Length,
                status,
                measurement.Status);
        }

        private static RoServerProfile ToServerProfile(IRoPacketProfile packetProfile) {
            var empty = new RoServerEndpoint(string.Empty, 0);
            return new RoServerProfile(
                packetProfile.Name,
                packetProfile.PacketVersion,
                packetProfile.ClientVersion,
                packetProfile.LanguageType,
                packetProfile.Renewal,
                packetProfile.PacketObfuscation,
                empty,
                empty,
                empty);
        }
    }

    public static class RoMapPacketReplayDecoder {
        public static RoMapEntryDrainResult DrainKnownServerMapPackets(
            RoPacketReplay replay,
            RoPacketRegistry registry = null,
            Encoding textEncoding = null,
            int maxFrames = int.MaxValue) {
            if (replay == null) {
                throw new ArgumentNullException(nameof(replay));
            }

            if (maxFrames <= 0) {
                return RoMapEntryDrainResult.Empty;
            }

            registry = registry ?? RoPacketCaptureProfileResolver.CreateRegistry(replay);
            var packets = new List<RoMapEntryDrainedPacket>();
            var playerChats = new List<RoZcNotifyPlayerChat>();
            var timeNotifications = new List<RoZcNotifyTime>();
            var actorEntries = new List<RoZcNotifyActorEntry>();
            var actorMoves = new List<RoZcNotifyMove>();
            var playerMoves = new List<RoZcNotifyPlayerMove>();
            var stopMoves = new List<RoZcStopMove>();
            var actorVanishes = new List<RoZcNotifyVanish>();
            var orderedEvents = new List<RoMapEntryDrainEvent>();
            bool stoppedOnUnsupportedPacket = false;
            string stopReason = string.Empty;

            while (packets.Count < maxFrames && replay.TryRead(out RoPacketCaptureEntry entry)) {
                if (entry.Direction != RoPacketDirection.ServerToClient) {
                    continue;
                }

                if (!entry.TryCreateFrame(registry, out RoPacketFrame frame, out RoPacketFrameMeasurement measurement)) {
                    if (entry.IsPacketFrame) {
                        packets.Add(new RoMapEntryDrainedPacket(
                            entry.PacketId,
                            entry.PacketName,
                            entry.Length,
                            measurement.Status));
                        stoppedOnUnsupportedPacket = true;
                        stopReason = $"Replay packet 0x{entry.PacketId:x4} could not be framed: {measurement.Status}.";
                        break;
                    }

                    continue;
                }

                packets.Add(new RoMapEntryDrainedPacket(frame.PacketId, frame.Definition.Name, frame.Length, RoPacketFrameStatus.Complete));
                switch (frame.PacketId) {
                    case RoPacketIds.ZcNotifyChat:
                        RoZcNotifyPlayerChat notifyChat = RoMapPacketParser.ParseZcNotifyChat(frame.Bytes, textEncoding);
                        playerChats.Add(notifyChat);
                        orderedEvents.Add(new RoMapEntryDrainEvent(notifyChat));
                        break;
                    case RoPacketIds.ZcNotifyPlayerChat:
                        RoZcNotifyPlayerChat playerChat = RoMapPacketParser.ParseZcNotifyPlayerChat(frame.Bytes, textEncoding);
                        playerChats.Add(playerChat);
                        orderedEvents.Add(new RoMapEntryDrainEvent(playerChat));
                        break;
                    case RoPacketIds.ZcNpcAckMapMove:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcAckMapMove(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcNpcAckServerMove:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcAckServerMove(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcNpcAckServerMoveDomain:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcAckServerMoveDomain(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcSayDialog:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogText, RoMapPacketParser.ParseZcSayDialog(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcWaitDialog:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogNext, RoMapPacketParser.ParseZcWaitDialog(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcCloseDialog:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogClose, RoMapPacketParser.ParseZcCloseDialog(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcMenuList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogMenu, RoMapPacketParser.ParseZcMenuList(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcOpenEditDlg:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogNumberInput, RoMapPacketParser.ParseZcOpenEditDlg(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcOpenEditDlgStr:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogTextInput, RoMapPacketParser.ParseZcOpenEditDlgStr(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcShowImage:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcShowImage(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcShowScript:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcShowScript(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcCompass:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcCompass(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcShowImage2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcShowImage2(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcClearDialog:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcDialogClear, RoMapPacketParser.ParseZcClearDialog(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcSelectDealType:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcSelectDealType(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPcPurchaseItemList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPcPurchaseItemList(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPcPurchaseItemList2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPcPurchaseItemList2(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPcSellItemList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPcSellItemList(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPcPurchaseResult:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcShopBuyResult, RoMapPacketParser.ParseZcPcPurchaseResult(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPcSellResult:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcShopSellResult, RoMapPacketParser.ParseZcPcSellResult(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNpcMarketOpen:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcMarketOpen(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNpcMarketOpen2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcMarketOpen2(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNpcMarketPurchaseResult:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcMarketPurchaseResult(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNpcMarketPurchaseResult2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcMarketPurchaseResult2(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNpcBarterMarketItemInfo:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcBarterList, RoMapPacketParser.ParseZcNpcBarterMarketItemInfo(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNpcExpandedBarterMarketItemInfo:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.NpcExpandedBarterList, RoMapPacketParser.ParseZcNpcExpandedBarterMarketItemInfo(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNotifyStoreItemCountInfo:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyStoreItemCountInfo(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAddItemToStore3:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAddItemToStore3(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcDeleteItemFromStore:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcDeleteItemFromStore(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcCloseStore:
                        RoMapPacketParser.ParseZcCloseStore(frame.Bytes);
                        orderedEvents.Add(RoMapEntryDrainEvent.StorageClosed());
                        break;
                    case RoPacketIds.ZcNotifyCartItemCountInfo:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyCartItemCountInfo(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcCartOff:
                        RoMapPacketParser.ParseZcCartOff(frame.Bytes);
                        orderedEvents.Add(RoMapEntryDrainEvent.CartOff());
                        break;
                    case RoPacketIds.ZcReqExchangeItem:
                    case RoPacketIds.ZcReqExchangeItem2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcReqExchangeItem(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckExchangeItem:
                    case RoPacketIds.ZcAckExchangeItem2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckExchangeItem(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAddExchangeItem:
                    case RoPacketIds.ZcAddExchangeItem2:
                    case RoPacketIds.ZcAddExchangeItem3:
                    case RoPacketIds.ZcAddExchangeItem4:
                    case RoPacketIds.ZcAddExchangeItem5:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAddExchangeItem(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckAddExchangeItem:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckAddExchangeItem(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcConcludeExchangeItem:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcConcludeExchangeItem(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcCancelExchangeItem:
                        RoMapPacketParser.ParseZcCancelExchangeItem(frame.Bytes);
                        orderedEvents.Add(RoMapEntryDrainEvent.TradeCancel());
                        break;
                    case RoPacketIds.ZcExecExchangeItem:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcExecExchangeItem(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcGuildBelongInfo:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildBelongInfo(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcGuildNotice:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildNotice(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcGuildMemberInfo:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildMemberInfo(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcGuildInvite:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildInvite(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcGuildInviteResult:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildInviteResult(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcGuildLeaveAck:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildLeaveAck(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcGuildChat:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcGuildChat(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcPetProperty:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPetProperty(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcFeedPet:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFeedPet(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPetChangeState:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPetChangeState(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPetAct:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPetAct(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcHomunculusProperty:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcHomunculusProperty(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcHomunculusChangeState:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.HomunculusChangeState, RoMapPacketParser.ParseZcHomunculusChangeState(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcHomunculusParChange:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcHomunculusParChange(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcHomunculusSkillInfoList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.HomunculusSkillInfoList, RoMapPacketParser.ParseZcHomunculusSkillInfoList(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcMercenaryInfo:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcMercenaryInfo(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcMercenarySkillInfoList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.MercenarySkillInfoList, RoMapPacketParser.ParseZcMercenarySkillInfoList(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcNotifyMapPropertyPk:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyMapPropertyPk(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNotifyRanking:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyRanking(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcBattlefieldNotifyPoint:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBattlefieldNotifyPoint(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcBattlefieldNotifyCampInfo:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBattlefieldNotifyCampInfo(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcBattlefieldNotifyPosition:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBattlefieldNotifyPosition(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcBattlefieldNotifyHpLegacy:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBattlefieldNotifyHpLegacy(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcBattlefieldNotifyHp:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcBattlefieldNotifyHp(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNotifyMapProperty:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyMapProperty(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNotifyMapProperty2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyMapProperty2(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNotifyTime:
                        RoZcNotifyTime timeNotification = RoMapPacketParser.ParseZcNotifyTime(frame.Bytes);
                        timeNotifications.Add(timeNotification);
                        orderedEvents.Add(new RoMapEntryDrainEvent(timeNotification));
                        break;
                    case RoPacketIds.ZcNotifyNewEntry11:
                        RoZcNotifyActorEntry newEntry = RoMapPacketParser.ParseZcNotifyNewEntry11(frame.Bytes, textEncoding);
                        actorEntries.Add(newEntry);
                        orderedEvents.Add(new RoMapEntryDrainEvent(newEntry));
                        break;
                    case RoPacketIds.ZcNotifyStandEntry11:
                        RoZcNotifyActorEntry standEntry = RoMapPacketParser.ParseZcNotifyStandEntry11(frame.Bytes, textEncoding);
                        actorEntries.Add(standEntry);
                        orderedEvents.Add(new RoMapEntryDrainEvent(standEntry));
                        break;
                    case RoPacketIds.ZcNotifyMoveEntry11:
                        RoZcNotifyActorEntry moveEntry = RoMapPacketParser.ParseZcNotifyMoveEntry11(frame.Bytes, textEncoding);
                        actorEntries.Add(moveEntry);
                        orderedEvents.Add(new RoMapEntryDrainEvent(moveEntry));
                        break;
                    case RoPacketIds.ZcNotifyMove:
                        RoZcNotifyMove actorMove = RoMapPacketParser.ParseZcNotifyMove(frame.Bytes);
                        actorMoves.Add(actorMove);
                        orderedEvents.Add(new RoMapEntryDrainEvent(actorMove));
                        break;
                    case RoPacketIds.ZcNotifyPlayerMove:
                        RoZcNotifyPlayerMove playerMove = RoMapPacketParser.ParseZcNotifyPlayerMove(frame.Bytes);
                        playerMoves.Add(playerMove);
                        orderedEvents.Add(new RoMapEntryDrainEvent(playerMove));
                        break;
                    case RoPacketIds.ZcStopMove:
                        RoZcStopMove stopMove = RoMapPacketParser.ParseZcStopMove(frame.Bytes);
                        stopMoves.Add(stopMove);
                        orderedEvents.Add(new RoMapEntryDrainEvent(stopMove));
                        break;
                    case RoPacketIds.ZcAckReqName:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqName(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckReqNameAll:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqNameAll(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckReqNameAll2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqNameAll2(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckReqNameAllNpc:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqNameAllNpc(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcHpInfo:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcHpInfo(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNotifyVanish:
                        RoZcNotifyVanish actorVanish = RoMapPacketParser.ParseZcNotifyVanish(frame.Bytes);
                        actorVanishes.Add(actorVanish);
                        orderedEvents.Add(new RoMapEntryDrainEvent(actorVanish));
                        break;
                    case RoPacketIds.ZcChangeDirection:
                        RoZcChangeDirection directionChange = RoMapPacketParser.ParseZcChangeDirection(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(directionChange));
                        break;
                    case RoPacketIds.ZcSpriteChange:
                        RoZcSpriteChange spriteChange = RoMapPacketParser.ParseZcSpriteChange(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(spriteChange));
                        break;
                    case RoPacketIds.ZcSpriteChange2:
                        RoZcSpriteChange spriteChange2 = RoMapPacketParser.ParseZcSpriteChange2(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(spriteChange2));
                        break;
                    case RoPacketIds.ZcItemEntry:
                        RoZcItemEntry itemEntry = RoMapPacketParser.ParseZcItemEntry(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(itemEntry));
                        break;
                    case RoPacketIds.ZcItemFallEntry:
                        RoZcItemEntry itemFallEntry = RoMapPacketParser.ParseZcItemFallEntry(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(itemFallEntry));
                        break;
                    case RoPacketIds.ZcItemFallEntry2:
                        RoZcItemEntry itemFallEntry2 = RoMapPacketParser.ParseZcItemFallEntry2(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(itemFallEntry2));
                        break;
                    case RoPacketIds.ZcItemFallEntry5:
                        RoZcItemEntry itemFallEntry5 = RoMapPacketParser.ParseZcItemFallEntry5(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(itemFallEntry5));
                        break;
                    case RoPacketIds.ZcItemDisappear:
                        RoZcItemDisappear itemDisappear = RoMapPacketParser.ParseZcItemDisappear(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(itemDisappear));
                        break;
                    case RoPacketIds.ZcInventoryStart:
                        RoZcInventoryStart inventoryStart = RoMapPacketParser.ParseZcInventoryStart(frame.Bytes, textEncoding);
                        orderedEvents.Add(new RoMapEntryDrainEvent(inventoryStart));
                        break;
                    case RoPacketIds.ZcInventoryListNormal:
                        RoZcInventoryItemList inventoryListNormal = RoMapPacketParser.ParseZcInventoryListNormal(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(inventoryListNormal));
                        break;
                    case RoPacketIds.ZcInventoryListNormal4:
                        RoZcInventoryItemList inventoryListNormal4 = RoMapPacketParser.ParseZcInventoryListNormal4(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(inventoryListNormal4));
                        break;
                    case RoPacketIds.ZcInventoryListEquip:
                        RoZcInventoryItemList inventoryListEquip = RoMapPacketParser.ParseZcInventoryListEquip(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(inventoryListEquip));
                        break;
                    case RoPacketIds.ZcInventoryListEquip4:
                        RoZcInventoryItemList inventoryListEquip4 = RoMapPacketParser.ParseZcInventoryListEquip4(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(inventoryListEquip4));
                        break;
                    case RoPacketIds.ZcInventoryListEquip5:
                        RoZcInventoryItemList inventoryListEquip5 = RoMapPacketParser.ParseZcInventoryListEquip5(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(inventoryListEquip5));
                        break;
                    case RoPacketIds.ZcInventoryListEquip2:
                        RoZcInventoryItemList inventoryListEquip2 = RoMapPacketParser.ParseZcInventoryListEquip2(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(inventoryListEquip2));
                        break;
                    case RoPacketIds.ZcInventoryEnd:
                        RoZcInventoryEnd inventoryEnd = RoMapPacketParser.ParseZcInventoryEnd(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(inventoryEnd));
                        break;
                    case RoPacketIds.ZcEquipSwitchList:
                        RoZcEquipSwitchList equipSwitchList = RoMapPacketParser.ParseZcEquipSwitchList(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(equipSwitchList));
                        break;
                    case RoPacketIds.ZcDeleteItemFromBody:
                        RoZcInventoryItemDelete inventoryDelete = RoMapPacketParser.ParseZcDeleteItemFromBody(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(inventoryDelete));
                        break;
                    case RoPacketIds.ZcAckWearEquipV5:
                        RoZcWearEquipmentAck wearEquipmentAck = RoMapPacketParser.ParseZcAckWearEquipV5(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(wearEquipmentAck));
                        break;
                    case RoPacketIds.ZcAckTakeoffEquipV5:
                        RoZcTakeoffEquipmentAck takeoffEquipmentAck = RoMapPacketParser.ParseZcAckTakeoffEquipV5(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(takeoffEquipmentAck));
                        break;
                    case RoPacketIds.ZcReqTakeoffEquipAck:
                        RoZcTakeoffEquipmentAck legacyTakeoffEquipmentAck = RoMapPacketParser.ParseZcReqTakeoffEquipAck(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(legacyTakeoffEquipmentAck));
                        break;
                    case RoPacketIds.ZcReqTakeoffEquipAck2:
                        RoZcTakeoffEquipmentAck legacyTakeoffEquipmentAck2 = RoMapPacketParser.ParseZcReqTakeoffEquipAck2(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(legacyTakeoffEquipmentAck2));
                        break;
                    case RoPacketIds.ZcEquipArrow:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcEquipArrow(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcMakingArrowList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcMakingArrowList(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcWarpList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcWarpList(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcWarpList2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcWarpList2(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNotifyMapInfo:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyMapInfo(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcItemThrowAck:
                        RoZcItemThrowAck itemThrowAck = RoMapPacketParser.ParseZcItemThrowAck(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(itemThrowAck));
                        break;
                    case RoPacketIds.ZcSkillDisappear:
                        RoZcSkillDisappear skillDisappear = RoMapPacketParser.ParseZcSkillDisappear(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillDisappear));
                        break;
                    case RoPacketIds.ZcDispel:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcDispel(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNotifyEffect2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyEffect2(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNotifyEffect3:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyEffect3(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNpcShowEfstUpdate:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNpcShowEfstUpdate(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckToUseSkill:
                        RoZcAckToUseSkill skillUseAck = RoMapPacketParser.ParseZcAckToUseSkill(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillUseAck));
                        break;
                    case RoPacketIds.ZcActionFailure:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcActionFailure(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcUseSkillAckLegacy:
                        RoZcUseSkillAck skillCastLegacy = RoMapPacketParser.ParseZcUseSkillAckLegacy(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillCastLegacy));
                        break;
                    case RoPacketIds.ZcUseSkillAck2:
                        RoZcUseSkillAck skillCast2 = RoMapPacketParser.ParseZcUseSkillAck2(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillCast2));
                        break;
                    case RoPacketIds.ZcUseSkillAck3:
                        RoZcUseSkillAck skillCast3 = RoMapPacketParser.ParseZcUseSkillAck3(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillCast3));
                        break;
                    case RoPacketIds.ZcNotifySkillLegacy:
                        RoZcNotifySkill skillDamageLegacy = RoMapPacketParser.ParseZcNotifySkillLegacy(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillDamageLegacy));
                        break;
                    case RoPacketIds.ZcNotifySkill2:
                        RoZcNotifySkill skillDamage2 = RoMapPacketParser.ParseZcNotifySkill2(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillDamage2));
                        break;
                    case RoPacketIds.ZcUseSkill:
                        RoZcUseSkillResult skillResult = RoMapPacketParser.ParseZcUseSkill(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillResult));
                        break;
                    case RoPacketIds.ZcNotifyEffect:
                        RoZcNotifyEffect notifyEffect = RoMapPacketParser.ParseZcNotifyEffect(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(notifyEffect));
                        break;
                    case RoPacketIds.ZcNotifyGroundSkill:
                        RoZcNotifyGroundSkill groundSkill = RoMapPacketParser.ParseZcNotifyGroundSkill(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(groundSkill));
                        break;
                    case RoPacketIds.ZcSkillPostDelay:
                        RoZcSkillPostDelay skillPostDelay = RoMapPacketParser.ParseZcSkillPostDelay(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillPostDelay));
                        break;
                    case RoPacketIds.ZcNotifySkillPosition:
                        RoZcNotifySkillPosition skillPosition = RoMapPacketParser.ParseZcNotifySkillPosition(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillPosition));
                        break;
                    case RoPacketIds.ZcSkillEntry:
                        RoZcSkillEntry skillEntryLegacy = RoMapPacketParser.ParseZcSkillEntry(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillEntryLegacy));
                        break;
                    case RoPacketIds.ZcSkillEntry2:
                        RoZcSkillEntry skillEntry2 = RoMapPacketParser.ParseZcSkillEntry2(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillEntry2));
                        break;
                    case RoPacketIds.ZcSkillEntry3:
                        RoZcSkillEntry skillEntry3 = RoMapPacketParser.ParseZcSkillEntry3(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillEntry3));
                        break;
                    case RoPacketIds.ZcSkillEntry4:
                        RoZcSkillEntry skillEntry4 = RoMapPacketParser.ParseZcSkillEntry4(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillEntry4));
                        break;
                    case RoPacketIds.ZcSkillEntry5:
                        RoZcSkillEntry skillEntry = RoMapPacketParser.ParseZcSkillEntry5(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(skillEntry));
                        break;
                    case RoPacketIds.ZcItemPickupAck7:
                        RoZcItemPickupAck itemPickupAck = RoMapPacketParser.ParseZcItemPickupAck7(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(itemPickupAck));
                        break;
                    case RoPacketIds.ZcItemPickupAck8:
                        RoZcItemPickupAck itemPickupAck8 = RoMapPacketParser.ParseZcItemPickupAck8(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(itemPickupAck8));
                        break;
                    case RoPacketIds.ZcUseItemAck:
                        RoZcUseItemAck useItemAck = RoMapPacketParser.ParseZcUseItemAck(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(useItemAck));
                        break;
                    case RoPacketIds.ZcUseItemAck2:
                        RoZcUseItemAck useItemAck2 = RoMapPacketParser.ParseZcUseItemAck2(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(useItemAck2));
                        break;
                    case RoPacketIds.ZcNotifyAct:
                        RoZcNotifyAct actorAction = RoMapPacketParser.ParseZcNotifyAct(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(actorAction));
                        break;
                    case RoPacketIds.ZcNotifyAct2:
                        RoZcNotifyAct actorAction2 = RoMapPacketParser.ParseZcNotifyAct2(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(actorAction2));
                        break;
                    case RoPacketIds.ZcNotifyAct3:
                        RoZcNotifyAct actorAction3 = RoMapPacketParser.ParseZcNotifyAct3(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(actorAction3));
                        break;
                    case RoPacketIds.ZcEmotion:
                        RoZcEmotion emotion = RoMapPacketParser.ParseZcEmotion(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(emotion));
                        break;
                    case RoPacketIds.ZcMsg:
                        RoZcMessage message = RoMapPacketParser.ParseZcMsg(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(message));
                        break;
                    case RoPacketIds.ZcMsgStateChange:
                        RoZcMsgStateChange statusChange = RoMapPacketParser.ParseZcMsgStateChange(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(statusChange));
                        break;
                    case RoPacketIds.ZcMsgStateChange2:
                        RoZcMsgStateChange statusChange2 = RoMapPacketParser.ParseZcMsgStateChange2(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(statusChange2));
                        break;
                    case RoPacketIds.ZcMsgStateChangeTimed:
                        RoZcMsgStateChange statusChangeTimed = RoMapPacketParser.ParseZcMsgStateChangeTimed(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(statusChangeTimed));
                        break;
                    case RoPacketIds.ZcMsgStateChange3:
                        RoZcMsgStateChange statusChange3 = RoMapPacketParser.ParseZcMsgStateChange3(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(statusChange3));
                        break;
                    case RoPacketIds.ZcMsgStateChangeTimedWithTotal:
                        RoZcMsgStateChange statusChangeTimedWithTotal = RoMapPacketParser.ParseZcMsgStateChangeTimedWithTotal(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(statusChangeTimedWithTotal));
                        break;
                    case RoPacketIds.ZcAutoRunSkill:
                        orderedEvents.Add(new RoMapEntryDrainEvent(
                            RoMapEntryDrainEventKind.AutoRunSkill,
                            RoMapPacketParser.ParseZcAutoRunSkill(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcSkillInfoUpdate:
                        orderedEvents.Add(new RoMapEntryDrainEvent(
                            RoMapEntryDrainEventKind.SkillInfoUpdate,
                            RoMapPacketParser.ParseZcSkillInfoUpdate(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAddSkill:
                        orderedEvents.Add(new RoMapEntryDrainEvent(
                            RoMapEntryDrainEventKind.SkillInfoAdd,
                            RoMapPacketParser.ParseZcAddSkill(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcSkillInfoUpdate2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(
                            RoMapEntryDrainEventKind.SkillInfoUpdate2,
                            RoMapPacketParser.ParseZcSkillInfoUpdate2(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcSkillInfoListLegacy:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcSkillInfoListLegacy(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcSkillInfoList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcSkillInfoList(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcShortcutKeyList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcShortcutKeyList(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcParChange:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcParChange(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcLongParChange:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcLongParChange(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcLongLongParChange:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcLongLongParChange(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcStatusChangeAck:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcStatusChangeAck(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcCoupleStatus:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcCoupleStatus(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcStatus:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcStatus(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcStatusChange:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcStatusChange(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcNotifyWeightPercentage:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcNotifyWeightPercentage(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAttackRange:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAttackRange(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcQuestList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestList(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcQuestAdd:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestAdd(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcQuestDelete:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestDelete(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcQuestActive:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestActive(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcQuestObjectiveUpdateLegacy:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestObjectiveUpdateLegacy(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcQuestHuntInfo:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestHuntInfo(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcQuestObjectiveUpdate:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestObjectiveUpdate(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcQuestObjectiveUpdateModern:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcQuestObjectiveUpdateModern(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPartyOptionLegacy:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyOptionLegacy(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPartyOption:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyOption(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPartyCreateAck:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyCreateAck(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPartyConfig:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyConfig(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPartyLeaderChanged:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyLeaderChanged(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPartyInfoLegacy:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInfoLegacy(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcPartyInfo2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInfo2(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcPartyInfo:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInfo(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcPartyMemberInfoLegacy:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyMemberInfoLegacy(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcPartyMemberInfo2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyMemberInfo2(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcPartyMemberInfo3:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyMemberInfo3(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcPartyMemberInfo:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyMemberInfo(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcPartyMemberLeave:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyMemberLeave(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcPartyPosition:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyPosition(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPartyHpLegacy:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyHpLegacy(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPartyHp:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyHp(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPartyAlive:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyAlive(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPartyJobAndLevel:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyJobAndLevel(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPartyInvite:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInvite(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcPartyInvite2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInvite2(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcPartyInviteAck:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInviteAck(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcPartyInviteAck2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPartyInviteAck2(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcFriendsList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFriendsList(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcFriendState:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFriendState(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckReqNameByGid:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqNameByGid(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckReqNameByGid2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqNameByGid2(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckCreateChatRoom:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.ChatRoomCreateAck, RoMapPacketParser.ParseZcChatRoomResult(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcRefuseEnterRoom:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.ChatRoomRefuse, RoMapPacketParser.ParseZcChatRoomResult(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcRoomNewEntry:
                    case RoPacketIds.ZcChangeChatRoom:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcChatRoomInfo(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcDestroyRoom:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcDestroyRoom(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcEnterRoom:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcEnterRoom(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcMemberNewEntry:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.ChatRoomMemberJoin, RoMapPacketParser.ParseZcChatRoomMember(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcMemberExit:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.ChatRoomMemberExit, RoMapPacketParser.ParseZcChatRoomMember(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcRoleChange:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcRoleChange(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckOpenBuyingStore:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreSetup, RoMapPacketParser.ParseZcBuyingStoreResult(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcFailedOpenBuyingStore:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreOpenFailed, RoMapPacketParser.ParseZcBuyingStoreResult(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcFailedTradeBuyingStoreToBuyer:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreBuyerFailed, RoMapPacketParser.ParseZcBuyingStoreResult(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcFailedTradeBuyingStoreToSeller:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreSellerFailed, RoMapPacketParser.ParseZcBuyingStoreResult(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcMyItemListBuyingStore:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreOwnList, RoMapPacketParser.ParseZcBuyingStoreList(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckItemListBuyingStore:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreItemList, RoMapPacketParser.ParseZcBuyingStoreList(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcBuyingStoreEntry:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreEntry, RoMapPacketParser.ParseZcBuyingStoreEntry(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcDisappearBuyingStoreEntry:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.BuyingStoreDisappear, RoMapPacketParser.ParseZcBuyingStoreEntry(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcDeleteItemFromBuyingStore: {
                        // sent instead of ZC_DELETE_ITEM_FROM_BODY (clif_buyingstore_delete_item): the inventory loses the item here too
                        RoZcBuyingStoreSold buyingStoreSold = RoMapPacketParser.ParseZcDeleteItemFromBuyingStore(frame.Bytes);
                        orderedEvents.Add(new RoMapEntryDrainEvent(buyingStoreSold));
                        orderedEvents.Add(new RoMapEntryDrainEvent(new RoZcInventoryItemDelete(0, buyingStoreSold.BagIndex, buyingStoreSold.Amount)));
                        break;
                    }
                    case RoPacketIds.ZcNotifyWeaponItemList:
                        orderedEvents.Add(RoMapEntryDrainEvent.WeaponRefineItems(RoMapPacketParser.ParseZcNotifyWeaponItemList(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckWeaponRefine:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckWeaponRefine(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcPetEggList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcPetEggList(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcStartCapture:
                        orderedEvents.Add(RoMapEntryDrainEvent.PetCapture(true, 0));
                        break;
                    case RoPacketIds.ZcTryCaptureMonster:
                        orderedEvents.Add(RoMapEntryDrainEvent.PetCapture(false, RoMapPacketParser.ParseZcTryCaptureMonster(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcMakableItemList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcMakableItemList(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckReqMakingItem:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReqMakingItem(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcMergeItemOpen:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcMergeItemOpen(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckMergeItem:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckMergeItem(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAutoSpellList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAutoSpellList(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcRepairItemList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcRepairItemList(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckItemRepair:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckItemRepair(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcUpdateItemFromBuyingStore:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcUpdateItemFromBuyingStore(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcFriendRequest:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFriendRequest(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcFriendAddResult:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFriendAddResult(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcFriendDelete:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcFriendDelete(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcUiOpen:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcUiOpen(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAttendanceResponse:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAttendanceResponse(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckRodexList:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckRodexList(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckRodexList2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckRodexList2(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckRodexList3:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckRodexList3(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckRodexList4:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckRodexList4(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckFailedAllRodexList:
                        RoMapPacketParser.ParseZcAckFailedAllRodexList(frame.Bytes);
                        orderedEvents.Add(RoMapEntryDrainEvent.RodexFailedList());
                        break;
                    case RoPacketIds.ZcAckReadRodex:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReadRodex(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckReadRodex2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckReadRodex2(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcAckSendRodex:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckSendRodex(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckZenyFromRodex:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.RodexZenyAck, RoMapPacketParser.ParseZcAckZenyFromRodex(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckItemFromRodex:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapEntryDrainEventKind.RodexItemsAck, RoMapPacketParser.ParseZcAckItemFromRodex(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckDeleteRodex:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckDeleteRodex(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckAddItemRodex:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckAddItemRodex(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckAddItemRodex2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckAddItemRodex2(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckRemoveRodexItem:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckRemoveRodexItem(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcAckOpenWriteRodex:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcAckOpenWriteRodex(frame.Bytes, textEncoding)));
                        break;
                    case RoPacketIds.ZcCheckReceiveCharacterName:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcCheckReceiveCharacterName(frame.Bytes)));
                        break;
                    case RoPacketIds.ZcCheckReceiveCharacterName2:
                        orderedEvents.Add(new RoMapEntryDrainEvent(RoMapPacketParser.ParseZcCheckReceiveCharacterName2(frame.Bytes, textEncoding)));
                        break;
                }
            }

            return new RoMapEntryDrainResult(
                packets.ToArray(),
                playerChats.ToArray(),
                timeNotifications.ToArray(),
                actorEntries.ToArray(),
                actorMoves.ToArray(),
                playerMoves.ToArray(),
                stopMoves.ToArray(),
                actorVanishes.ToArray(),
                orderedEvents.ToArray(),
                stoppedOnUnsupportedPacket,
                stopReason);
        }
    }
}
