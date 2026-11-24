using System;
using System.Threading.Tasks;
using Ragnarok.Network;

namespace Ragnarok.UI {
    public interface IRoChatCommandExecutor {
        Task<RoChatCommandResult> SendChatAsync(RoChatChannel channel, string message, string target = "");
    }
    public sealed class RoLegacyChatCommandExecutor : IRoChatCommandExecutor {
        private readonly RoLoginShellSession session;

        public RoLegacyChatCommandExecutor(RoLoginShellSession session) {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public async Task<RoChatCommandResult> SendChatAsync(RoChatChannel channel, string message, string target = "") {
            if (channel == RoChatChannel.Whisper) {
                if (string.IsNullOrWhiteSpace(target)) {
                    return new RoChatCommandResult(
                        RoChatCommandStatus.MissingWhisperTarget,
                        channel,
                        message,
                        target,
                        "Whisper target is required.");
                }

                await session.BeginSendPrivateMessage(target, message).ConfigureAwait(false);
                return new RoChatCommandResult(RoChatCommandStatus.Accepted, channel, message, target);
            }

            if (channel == RoChatChannel.Party) {
                await session.BeginSendPartyChatMessage(message).ConfigureAwait(false);
                return new RoChatCommandResult(RoChatCommandStatus.Accepted, channel, message, target);
            }

            if (channel == RoChatChannel.Guild) {
                await session.BeginSendGuildChatMessage(message).ConfigureAwait(false);
                return new RoChatCommandResult(RoChatCommandStatus.Accepted, channel, message, target);
            }

            if (channel == RoChatChannel.Clan) {
                await session.BeginSendClanChatMessage(message).ConfigureAwait(false);
                return new RoChatCommandResult(RoChatCommandStatus.Accepted, channel, message, target);
            }

            if (channel != RoChatChannel.Main) {
                return new RoChatCommandResult(
                    RoChatCommandStatus.UnsupportedChannel,
                    channel,
                    message,
                    target,
                    $"{channel} chat is not supported by this shell command executor.");
            }

            await session.BeginSendChatMessage(message).ConfigureAwait(false);
            return new RoChatCommandResult(RoChatCommandStatus.Accepted, channel, message, target);
        }
    }
}
