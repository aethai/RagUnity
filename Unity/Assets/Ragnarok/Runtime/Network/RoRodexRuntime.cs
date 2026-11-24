using System;
using System.Collections.Generic;

namespace Ragnarok.Network {
    public readonly struct RoRodexMailSnapshot {
        public RoRodexMailSnapshot(byte openType, ulong mailId, bool isRead, byte type, string senderName, int expireDateTime, string title) {
            OpenType = openType;
            MailId = mailId;
            IsRead = isRead;
            Type = type;
            SenderName = senderName ?? string.Empty;
            ExpireDateTime = expireDateTime;
            Title = title ?? string.Empty;
        }

        public byte OpenType { get; }
        public ulong MailId { get; }
        public bool IsRead { get; }
        public byte Type { get; }
        public string SenderName { get; }
        public int ExpireDateTime { get; }
        public string Title { get; }
        public bool HasZeny => (Type & 2) != 0;
        public bool HasItems => (Type & 4) != 0;
    }

    public readonly struct RoRodexReadSnapshot {
        public RoRodexReadSnapshot(byte openType, ulong mailId, string body, long zeny, RoZcInventoryItem[] items) {
            OpenType = openType;
            MailId = mailId;
            Body = body ?? string.Empty;
            Zeny = Math.Max(0, zeny);
            Items = Copy(items);
        }

        public static readonly RoRodexReadSnapshot Empty = new RoRodexReadSnapshot(0, 0, string.Empty, 0, Array.Empty<RoZcInventoryItem>());

        public byte OpenType { get; }
        public ulong MailId { get; }
        public string Body { get; }
        public long Zeny { get; }
        public RoZcInventoryItem[] Items { get; }
        public bool HasMail => MailId != 0;

        public RoRodexReadSnapshot WithoutZeny() {
            return new RoRodexReadSnapshot(OpenType, MailId, Body, 0, Items);
        }

        public RoRodexReadSnapshot WithoutItems() {
            return new RoRodexReadSnapshot(OpenType, MailId, Body, Zeny, Array.Empty<RoZcInventoryItem>());
        }

        private static RoZcInventoryItem[] Copy(RoZcInventoryItem[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoZcInventoryItem>();
            }

            var copy = new RoZcInventoryItem[source.Length];
            Array.Copy(source, copy, copy.Length);
            return copy;
        }
    }

    public readonly struct RoRodexComposeAttachmentSnapshot {
        public RoRodexComposeAttachmentSnapshot(RoZcInventoryItem item, int amount) {
            Item = item;
            Amount = Math.Max(0, amount);
        }

        public RoZcInventoryItem Item { get; }
        public int Amount { get; }
        public int BagIndex => Item.BagIndex;

        public RoRodexComposeAttachmentSnapshot WithAmount(int amount) {
            return new RoRodexComposeAttachmentSnapshot(Item, amount);
        }
    }

    public readonly struct RoRodexComposeSnapshot {
        public RoRodexComposeSnapshot(
            bool isOpen,
            string receiverName,
            uint receiverCharacterId,
            short receiverJob,
            short receiverLevel,
            bool receiverValidated,
            RoRodexComposeAttachmentSnapshot[] attachments,
            ushort weight,
            string lastMessage) {
            IsOpen = isOpen;
            ReceiverName = receiverName ?? string.Empty;
            ReceiverCharacterId = receiverCharacterId;
            ReceiverJob = receiverJob;
            ReceiverLevel = receiverLevel;
            ReceiverValidated = receiverValidated;
            Attachments = Copy(attachments);
            Weight = weight;
            LastMessage = lastMessage ?? string.Empty;
        }

        public static readonly RoRodexComposeSnapshot Empty = new RoRodexComposeSnapshot(false, string.Empty, 0, 0, 0, false, Array.Empty<RoRodexComposeAttachmentSnapshot>(), 0, string.Empty);

        public bool IsOpen { get; }
        public string ReceiverName { get; }
        public uint ReceiverCharacterId { get; }
        public short ReceiverJob { get; }
        public short ReceiverLevel { get; }
        public bool ReceiverValidated { get; }
        public RoRodexComposeAttachmentSnapshot[] Attachments { get; }
        public ushort Weight { get; }
        public string LastMessage { get; }

        private static RoRodexComposeAttachmentSnapshot[] Copy(RoRodexComposeAttachmentSnapshot[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoRodexComposeAttachmentSnapshot>();
            }

            var copy = new RoRodexComposeAttachmentSnapshot[source.Length];
            Array.Copy(source, copy, copy.Length);
            return copy;
        }
    }

    public readonly struct RoRodexSnapshot {
        public RoRodexSnapshot(
            bool isOpen,
            byte activeOpenType,
            bool isEnd,
            RoRodexMailSnapshot[] mails,
            RoRodexReadSnapshot read,
            RoRodexComposeSnapshot compose,
            int sequence,
            string lastMessage,
            int ackSerial = 0,
            RoRodexAckKind ackKind = RoRodexAckKind.None,
            byte ackResult = 0) {
            AckSerial = ackSerial;
            AckKind = ackKind;
            AckResult = ackResult;
            IsOpen = isOpen;
            ActiveOpenType = activeOpenType;
            IsEnd = isEnd;
            Mails = Copy(mails);
            Read = read;
            Compose = compose;
            Sequence = sequence;
            LastMessage = lastMessage ?? string.Empty;
        }

        public static readonly RoRodexSnapshot Empty = new RoRodexSnapshot(false, 0, true, Array.Empty<RoRodexMailSnapshot>(), RoRodexReadSnapshot.Empty, RoRodexComposeSnapshot.Empty, 0, string.Empty);

        public bool IsOpen { get; }
        public byte ActiveOpenType { get; }
        public bool IsEnd { get; }
        public RoRodexMailSnapshot[] Mails { get; }
        public RoRodexReadSnapshot Read { get; }
        public RoRodexComposeSnapshot Compose { get; }
        public int Sequence { get; }
        public string LastMessage { get; }
        /// <summary>The last result packet (0x09ed send, 0x09f4 items, 0x09f2 zeny, 0x0a51 name) with a counter.</summary>
        public int AckSerial { get; }
        public RoRodexAckKind AckKind { get; }
        public byte AckResult { get; }

        private static RoRodexMailSnapshot[] Copy(RoRodexMailSnapshot[] source) {
            if (source == null || source.Length == 0) {
                return Array.Empty<RoRodexMailSnapshot>();
            }

            var copy = new RoRodexMailSnapshot[source.Length];
            Array.Copy(source, copy, copy.Length);
            return copy;
        }
    }

    public enum RoRodexAckKind {
        None,
        Send,
        Items,
        Zeny,
        /// <summary>ZC_CHECKNAME with char id 0: result 1.</summary>
        NameCheck
    }

    public sealed class RoRodexRuntime : IDisposable {
        private readonly object sync = new object();
        private readonly Dictionary<ulong, RoRodexMailSnapshot> mailsById = new Dictionary<ulong, RoRodexMailSnapshot>();
        private readonly List<RoRodexComposeAttachmentSnapshot> attachments = new List<RoRodexComposeAttachmentSnapshot>(5);
        private bool disposed;
        private bool isOpen;
        private bool isEnd = true;
        private byte activeOpenType;
        private RoRodexReadSnapshot read = RoRodexReadSnapshot.Empty;
        private bool composeOpen;
        private string receiverName = string.Empty;
        private uint receiverCharacterId;
        private short receiverJob;
        private short receiverLevel;
        private bool receiverValidated;
        private ushort composeWeight;
        private int sequence;
        private string lastMessage = string.Empty;
        private int ackSerial;
        private RoRodexAckKind ackKind;
        private byte ackResult;

        private void AckLocked(RoRodexAckKind kind, byte result) {
            ackSerial++;
            ackKind = kind;
            ackResult = result;
        }

        public event EventHandler Changed;

        public RoRodexSnapshot Snapshot() {
            lock (sync) {
                return BuildSnapshotLocked();
            }
        }

        /// <summary>The next snapshot's Sequence, without copying the mails.</summary>
        public int CurrentSequence {
            get {
                lock (sync) {
                    return sequence;
                }
            }
        }

        public void OpenListLocal() {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                if (!isOpen || string.IsNullOrWhiteSpace(lastMessage)) {
                    lastMessage = "Opening Rodex...";
                    sequence++;
                    changed = true;
                }
            }

            RaiseChanged(changed);
        }

        public void ApplyListFailed(bool openWindow = true) {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                isOpen = openWindow;
                isEnd = true;
                read = RoRodexReadSnapshot.Empty;
                lastMessage = "Rodex mail list failed.";
                sequence++;
                changed = true;
            }

            RaiseChanged(changed);
        }

        public bool ApplyOpenRejectedIfPending(string message) {
            bool changed = false;
            lock (sync) {
                if (disposed || isOpen || !IsRodexOpeningLocked() || !ContainsRodex(message)) {
                    return false;
                }

                isEnd = true;
                read = RoRodexReadSnapshot.Empty;
                lastMessage = string.IsNullOrWhiteSpace(message) ? "Rodex open rejected." : message.Trim();
                sequence++;
                changed = true;
            }

            RaiseChanged(changed);
            return changed;
        }

        private bool IsRodexOpeningLocked() {
            return string.Equals(lastMessage, "Opening Rodex...", StringComparison.Ordinal);
        }

        private static bool ContainsRodex(string message) {
            return !string.IsNullOrWhiteSpace(message) &&
                message.IndexOf("rodex", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void Apply(RoZcRodexMailList list) {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                isOpen = true;
                isEnd = list.IsEnd;
                RoRodexMailHeader[] mails = list.Mails ?? Array.Empty<RoRodexMailHeader>();
                if (mails.Length > 0) {
                    activeOpenType = mails[0].OpenType;
                }

                for (int i = 0; i < mails.Length; i++) {
                    RoRodexMailHeader mail = mails[i];
                    mailsById[mail.MailId] = new RoRodexMailSnapshot(
                        mail.OpenType,
                        mail.MailId,
                        mail.IsRead,
                        mail.Type,
                        mail.SenderName,
                        mail.ExpireDateTime,
                        mail.Title);
                }

                sequence++;
                lastMessage = "Rodex mail list updated.";
                changed = true;
            }

            RaiseChanged(changed);
        }

        public void Apply(RoZcRodexReadMail packet) {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                isOpen = true;
                activeOpenType = packet.OpenType;
                read = new RoRodexReadSnapshot(packet.OpenType, packet.MailId, packet.Body, packet.Zeny, packet.Items);
                ClearComposeLocked();
                if (mailsById.TryGetValue(packet.MailId, out RoRodexMailSnapshot existing)) {
                    mailsById[packet.MailId] = new RoRodexMailSnapshot(
                        existing.OpenType,
                        existing.MailId,
                        isRead: true,
                        existing.Type,
                        existing.SenderName,
                        existing.ExpireDateTime,
                        existing.Title);
                }

                sequence++;
                lastMessage = "Rodex mail opened.";
                changed = true;
            }

            RaiseChanged(changed);
        }

        public bool CloseReadLocal() {
            bool changed = false;
            lock (sync) {
                if (disposed || !read.HasMail) {
                    return false;
                }

                read = RoRodexReadSnapshot.Empty;
                sequence++;
                changed = true;
            }

            RaiseChanged(changed);
            return changed;
        }

        public void Apply(RoZcRodexAttachmentAck packet, bool zeny) {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                if (packet.Success && read.MailId == packet.MailId) {
                    read = zeny ? read.WithoutZeny() : read.WithoutItems();
                }

                if (packet.Success) {
                    ClearMailAttachmentTypeLocked(packet.MailId, zeny ? (byte) 2 : (byte) 4);
                }

                AckLocked(zeny ? RoRodexAckKind.Zeny : RoRodexAckKind.Items, packet.Result);

                sequence++;
                lastMessage = packet.Success
                    ? zeny ? "Rodex zeny received." : "Rodex items received."
                    : zeny ? "Rodex zeny receive failed." : "Rodex item receive failed.";
                changed = true;
            }

            RaiseChanged(changed);
        }

        private void ClearMailAttachmentTypeLocked(ulong mailId, byte mask) {
            if (!mailsById.TryGetValue(mailId, out RoRodexMailSnapshot existing)) {
                return;
            }

            byte type = (byte) (existing.Type & ~mask);
            if (type == existing.Type) {
                return;
            }

            mailsById[mailId] = new RoRodexMailSnapshot(
                existing.OpenType,
                existing.MailId,
                existing.IsRead,
                type,
                existing.SenderName,
                existing.ExpireDateTime,
                existing.Title);
        }

        public void Apply(RoZcRodexDeleteAck packet) {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                mailsById.Remove(packet.MailId);
                if (read.MailId == packet.MailId) {
                    read = RoRodexReadSnapshot.Empty;
                }

                sequence++;
                lastMessage = "Rodex mail deleted.";
                changed = true;
            }

            RaiseChanged(changed);
        }

        public void Apply(RoZcRodexSendAck packet) {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                lastMessage = packet.Success ? "Rodex mail sent." : "Rodex send failed.";
                AckLocked(RoRodexAckKind.Send, packet.Result);
                if (packet.Success) {
                    ClearComposeLocked();
                }

                sequence++;
                changed = true;
            }

            RaiseChanged(changed);
        }

        public void Apply(RoZcRodexOpenWriteAck packet) {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                composeOpen = packet.Success;
                receiverName = string.IsNullOrWhiteSpace(packet.ReceiverName) ? receiverName : packet.ReceiverName.Trim();
                lastMessage = packet.Success ? "Rodex writer opened." : "Rodex writer failed to open.";
                sequence++;
                changed = true;
            }

            RaiseChanged(changed);
        }

        public void Apply(RoZcRodexCheckNameAck packet) {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                receiverCharacterId = packet.CharacterId;
                receiverJob = packet.Job;
                receiverLevel = packet.Level;
                if (!string.IsNullOrWhiteSpace(packet.CharacterName)) {
                    receiverName = packet.CharacterName.Trim();
                }

                receiverValidated = packet.Success;
                if (!packet.Success) AckLocked(RoRodexAckKind.NameCheck, 1);
                lastMessage = packet.Success ? "Rodex receiver validated." : "Rodex receiver not found.";
                sequence++;
                changed = true;
            }

            RaiseChanged(changed);
        }

        public void CheckReceiverLocal(string name) {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                composeOpen = true;
                receiverName = SanitizeReceiverName(name);
                receiverCharacterId = 0;
                receiverJob = 0;
                receiverLevel = 0;
                receiverValidated = false;
                lastMessage = "Checking Rodex receiver.";
                sequence++;
                changed = true;
            }

            RaiseChanged(changed);
        }

        public void Apply(RoZcRodexAddItemAck packet) {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                if (packet.Success) {
                    UpsertAttachmentLocked(packet.Item, packet.Count);
                    composeWeight = packet.Weight;
                }

                lastMessage = packet.Success ? "Rodex attachment added." : "Rodex attachment add failed.";
                sequence++;
                changed = true;
            }

            RaiseChanged(changed);
        }

        public void Apply(RoZcRodexRemoveItemAck packet) {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                if (packet.Success) {
                    RemoveAttachmentLocked(packet.BagIndex, packet.Count);
                    composeWeight = packet.Weight;
                }

                lastMessage = packet.Success ? "Rodex attachment removed." : "Rodex attachment remove failed.";
                sequence++;
                changed = true;
            }

            RaiseChanged(changed);
        }

        public void OpenComposeLocal(string name) {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                composeOpen = true;
                receiverName = SanitizeReceiverName(name);
                receiverCharacterId = 0;
                receiverValidated = false;
                lastMessage = "Rodex writer opened locally.";
                sequence++;
                changed = true;
            }

            RaiseChanged(changed);
        }

        public bool CloseComposeLocal() {
            bool changed = false;
            lock (sync) {
                if (disposed || !composeOpen) {
                    return false;
                }

                ClearComposeLocked();
                lastMessage = "Rodex writer closed.";
                sequence++;
                changed = true;
            }

            RaiseChanged(changed);
            return changed;
        }

        public void CloseLocal() {
            bool changed = false;
            lock (sync) {
                if (disposed) {
                    return;
                }

                isOpen = false;
                read = RoRodexReadSnapshot.Empty;
                ClearComposeLocked();
                lastMessage = "Rodex closed.";
                sequence++;
                changed = true;
            }

            RaiseChanged(changed);
        }

        public static string SanitizeReceiverName(string value) {
            value = (value ?? string.Empty).Trim().Replace("\t", string.Empty);
            while (value.Length > 0 && (value[0] == '$' || value[0] == '%')) {
                value = value.Substring(1).TrimStart();
            }

            return value;
        }

        public void Dispose() {
            lock (sync) {
                disposed = true;
                mailsById.Clear();
                attachments.Clear();
                read = RoRodexReadSnapshot.Empty;
                composeWeight = 0;
                sequence = 0;
                lastMessage = string.Empty;
            }
        }

        private RoRodexSnapshot BuildSnapshotLocked() {
            var mails = new RoRodexMailSnapshot[mailsById.Count];
            mailsById.Values.CopyTo(mails, 0);
            Array.Sort(mails, (left, right) => right.MailId.CompareTo(left.MailId));
            return new RoRodexSnapshot(
                isOpen,
                activeOpenType,
                isEnd,
                mails,
                read,
                new RoRodexComposeSnapshot(
                    composeOpen,
                    receiverName,
                    receiverCharacterId,
                    receiverJob,
                    receiverLevel,
                    receiverValidated,
                    attachments.ToArray(),
                    composeWeight,
                    lastMessage),
                sequence,
                lastMessage,
                ackSerial,
                ackKind,
                ackResult);
        }

        private void ClearComposeLocked() {
            composeOpen = false;
            receiverName = string.Empty;
            receiverCharacterId = 0;
            receiverJob = 0;
            receiverLevel = 0;
            receiverValidated = false;
            attachments.Clear();
            composeWeight = 0;
        }

        private void UpsertAttachmentLocked(RoZcInventoryItem item, int count) {
            for (int i = 0; i < attachments.Count; i++) {
                if (attachments[i].BagIndex == item.BagIndex) {
                    attachments[i] = attachments[i].WithAmount(Math.Max(1, count));
                    return;
                }
            }

            if (attachments.Count < 5) {
                attachments.Add(new RoRodexComposeAttachmentSnapshot(item, Math.Max(1, count)));
            }
        }

        private void RemoveAttachmentLocked(int bagIndex, int count) {
            for (int i = 0; i < attachments.Count; i++) {
                if (attachments[i].BagIndex != bagIndex) {
                    continue;
                }

                int remaining = attachments[i].Amount - Math.Max(1, count);
                if (remaining > 0) {
                    attachments[i] = attachments[i].WithAmount(remaining);
                } else {
                    attachments.RemoveAt(i);
                }

                return;
            }
        }

        private void RaiseChanged(bool changed) {
            if (changed) {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
