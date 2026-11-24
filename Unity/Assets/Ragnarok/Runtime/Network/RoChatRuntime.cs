using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ragnarok.Network {
    public enum RoChatChannel {
        All = 0,
        Main = 1,
        Party = 2,
        Guild = 3,
        Whisper = 4,
        System = 5,
        Clan = 6,
        Item = 7
    }

    public enum RoChatCommandStatus {
        Accepted = 0,
        InactiveMapSession = 1,
        EmptyMessage = 2,
        MessageTooLong = 3,
        MissingWhisperTarget = 4,
        UnsupportedChannel = 5,
        SendFailed = 6
    }

    public readonly struct RoChatMessage {
        public RoChatMessage(
            long sequence,
            RoChatChannel channel,
            string sender,
            string text,
            uint sourceActorId = 0,
            bool isSystem = false) {
            Sequence = sequence;
            Channel = channel;
            Sender = sender ?? string.Empty;
            Text = text ?? string.Empty;
            SourceActorId = sourceActorId;
            IsSystem = isSystem;
        }

        public long Sequence { get; }
        public RoChatChannel Channel { get; }
        public string Sender { get; }
        public string Text { get; }
        public uint SourceActorId { get; }
        public bool IsSystem { get; }
    }

    public readonly struct RoChatSendRequest {
        public RoChatSendRequest(RoChatChannel channel, string message, string target = "") {
            Channel = channel;
            Message = message ?? string.Empty;
            Target = target ?? string.Empty;
        }

        public RoChatChannel Channel { get; }
        public string Message { get; }
        public string Target { get; }
    }

    public readonly struct RoChatCommandResult {
        public RoChatCommandResult(RoChatCommandStatus status, RoChatChannel channel, string message, string target, string diagnostic = "") {
            Status = status;
            Channel = channel;
            Message = message ?? string.Empty;
            Target = target ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RoChatCommandStatus Status { get; }
        public RoChatChannel Channel { get; }
        public string Message { get; }
        public string Target { get; }
        public string Diagnostic { get; }
        public bool IsAccepted => Status == RoChatCommandStatus.Accepted;
    }

    public sealed class RoChatRuntime : IDisposable {
        private const int DefaultMaxMessages = 400;
        private const int MaxWireMessageLength = 255;

        private readonly object sync = new object();
        private readonly List<RoChatMessage> messages = new List<RoChatMessage>(64);
        private readonly Func<RoChatSendRequest, CancellationToken, Task> sendChat;
        private long nextSequence;
        private bool disposed;

        public RoChatRuntime(Func<RoChatSendRequest, CancellationToken, Task> sendChat) {
            this.sendChat = sendChat;
        }

        public int MessageCount {
            get {
                lock (sync) {
                    return messages.Count;
                }
            }
        }

        public void Apply(RoZcNotifyPlayerChat chat) {
            AddMessage(RoChatChannel.Main, string.Empty, chat.Message, chat.SourceActorId);
        }

        public void Apply(RoZcWhisper whisper) {
            AddMessage(RoChatChannel.Whisper, whisper.Sender, whisper.Message);
        }

        public void Apply(RoZcGuildChat chat) {
            AddMessage(RoChatChannel.Guild, string.Empty, chat.Message);
        }

        public void Apply(RoZcPartyChat chat) {
            AddMessage(RoChatChannel.Party, string.Empty, chat.Message);
        }

        public void Apply(RoZcClanChat chat) {
            AddMessage(RoChatChannel.Clan, chat.Sender, chat.Message);
        }

        public void AddSystemMessage(string text) {
            AddMessage(RoChatChannel.System, string.Empty, text, isSystem: true);
        }

        public RoChatMessage[] SnapshotMessages(RoChatChannel channel = RoChatChannel.All) {
            lock (sync) {
                if (channel == RoChatChannel.All) {
                    return messages.ToArray();
                }

                var filtered = new List<RoChatMessage>();
                for (int i = 0; i < messages.Count; i++) {
                    if (messages[i].Channel == channel) {
                        filtered.Add(messages[i]);
                    }
                }

                return filtered.ToArray();
            }
        }

        public async Task<RoChatCommandResult> SendChatAsync(
            RoChatChannel channel,
            string message,
            string target = "",
            CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            string normalized = NormalizeMessage(message);
            string normalizedTarget = (target ?? string.Empty).Trim();
            if (normalized.Length == 0) {
                return new RoChatCommandResult(RoChatCommandStatus.EmptyMessage, channel, normalized, normalizedTarget, "Chat message is empty.");
            }

            if (normalized.Length > MaxWireMessageLength) {
                return new RoChatCommandResult(
                    RoChatCommandStatus.MessageTooLong,
                    channel,
                    normalized,
                    normalizedTarget,
                    $"Chat message exceeds {MaxWireMessageLength} characters.");
            }

            if (channel == RoChatChannel.Whisper && string.IsNullOrWhiteSpace(normalizedTarget)) {
                return new RoChatCommandResult(
                    RoChatCommandStatus.MissingWhisperTarget,
                    channel,
                    normalized,
                    normalizedTarget,
                    "Whisper target is required.");
            }

            if (sendChat == null) {
                return new RoChatCommandResult(RoChatCommandStatus.InactiveMapSession, channel, normalized, normalizedTarget, "Map runtime session is not active.");
            }

            if (channel != RoChatChannel.Main &&
                channel != RoChatChannel.Party &&
                channel != RoChatChannel.Guild &&
                channel != RoChatChannel.Whisper &&
                channel != RoChatChannel.Clan) {
                AddSystemMessage($"{channel} chat is not wired for outbound packet send in this profile yet.");
                return new RoChatCommandResult(
                    RoChatCommandStatus.UnsupportedChannel,
                    channel,
                    normalized,
                    normalizedTarget,
                    $"{channel} chat send is not supported by the current S13 packet set.");
            }

            try {
                await sendChat(new RoChatSendRequest(channel, normalized, normalizedTarget), cancellationToken).ConfigureAwait(false);
                return new RoChatCommandResult(RoChatCommandStatus.Accepted, channel, normalized, normalizedTarget);
            } catch (Exception ex) when (!(ex is OperationCanceledException)) {
                return new RoChatCommandResult(RoChatCommandStatus.SendFailed, channel, normalized, normalizedTarget, ex.Message);
            }
        }

        public void Dispose() {
            disposed = true;
            lock (sync) {
                messages.Clear();
            }
        }

        private void AddMessage(RoChatChannel channel, string sender, string text, uint sourceActorId = 0, bool isSystem = false) {
            if (string.IsNullOrWhiteSpace(text)) {
                return;
            }

            lock (sync) {
                messages.Add(new RoChatMessage(++nextSequence, channel, sender, text.Trim(), sourceActorId, isSystem));
                int removeCount = messages.Count - DefaultMaxMessages;
                if (removeCount > 0) {
                    messages.RemoveRange(0, removeCount);
                }
            }
        }

        private void ThrowIfDisposed() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(RoChatRuntime));
            }
        }

        private static string NormalizeMessage(string message) {
            return string.IsNullOrWhiteSpace(message)
                ? string.Empty
                : message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }
}
